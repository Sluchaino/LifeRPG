using System.Security.Claims;
using Domain.Progression;

namespace LifeRPG.API.Endpoints
{
    public static class CalendarEndpoints
    {
        public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
        {
            var calendarGroup = app.MapGroup("/api/calendar").RequireAuthorization();
            calendarGroup.MapGet("/tasks", GetTasksAsync);
            calendarGroup.MapGet("/tasks/range", GetTasksRangeAsync);
            calendarGroup.MapPatch("/tasks/{id:guid}", UpdateTaskAsync);

            var tasksGroup = app.MapGroup("/api/tasks").RequireAuthorization();
            tasksGroup.MapGet("", GetTasksListAsync);
            tasksGroup.MapPost("", CreateTaskAsync);
            tasksGroup.MapPatch("/{id:guid}", UpdateTaskAsync);
            tasksGroup.MapDelete("/{id:guid}", DeleteTaskAsync);

            return app;
        }

        private static async Task<IResult> GetTasksAsync(
            string date,
            ClaimsPrincipal principal,
            AppDbContext dbContext,
            ICurrentUserProvider currentUserProvider,
            CancellationToken cancellationToken)
        {
            var userId = currentUserProvider.GetUserId(principal);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            if (!DateOnly.TryParse(date, out var parsedDate))
            {
                return Results.BadRequest(new { error = "Неверная дата." });
            }

            var tasks = await dbContext.CalendarTasks
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value && x.Date == parsedDate)
                .Include(x => x.Attributes)
                .Include(x => x.Skills)
                .Include(x => x.Habit)
                .OrderBy(x => x.StartTime ?? TimeOnly.MaxValue)
                .ThenBy(x => x.Title)
                .ToListAsync(cancellationToken);

            return Results.Ok(tasks.Select(MapTaskResponse).ToList());
        }

        private static async Task<IResult> GetTasksRangeAsync(
            string from,
            string to,
            ClaimsPrincipal principal,
            AppDbContext dbContext,
            ICurrentUserProvider currentUserProvider,
            CancellationToken cancellationToken)
        {
            var userId = currentUserProvider.GetUserId(principal);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            if (!DateOnly.TryParse(from, out var fromDate) ||
                !DateOnly.TryParse(to, out var toDate))
            {
                return Results.BadRequest(new { error = "Неверный диапазон дат." });
            }

            if (toDate < fromDate)
            {
                return Results.BadRequest(new { error = "Дата окончания должна быть позже даты начала." });
            }

            var tasks = await dbContext.CalendarTasks
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value && x.Date >= fromDate && x.Date <= toDate)
                .Include(x => x.Attributes)
                .Include(x => x.Skills)
                .Include(x => x.Habit)
                .OrderBy(x => x.Date)
                .ThenBy(x => x.StartTime ?? TimeOnly.MaxValue)
                .ThenBy(x => x.Title)
                .ToListAsync(cancellationToken);

            return Results.Ok(tasks.Select(MapTaskResponse).ToList());
        }

        private static async Task<IResult> GetTasksListAsync(
            string? date,
            string? from,
            string? to,
            ClaimsPrincipal principal,
            AppDbContext dbContext,
            ICurrentUserProvider currentUserProvider,
            CancellationToken cancellationToken)
        {
            var userId = currentUserProvider.GetUserId(principal);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            DateOnly? targetDate = null;
            DateOnly? fromDate = null;
            DateOnly? toDate = null;

            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!DateOnly.TryParse(date, out var parsedDate))
                {
                    return Results.BadRequest(new { error = "Неверная дата." });
                }

                targetDate = parsedDate;
            }

            if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
            {
                if (string.IsNullOrWhiteSpace(from) ||
                    string.IsNullOrWhiteSpace(to) ||
                    !DateOnly.TryParse(from, out var parsedFrom) ||
                    !DateOnly.TryParse(to, out var parsedTo))
                {
                    return Results.BadRequest(new { error = "Неверный диапазон дат." });
                }

                if (parsedTo < parsedFrom)
                {
                    return Results.BadRequest(new { error = "Дата окончания должна быть позже даты начала." });
                }

                fromDate = parsedFrom;
                toDate = parsedTo;
            }

            if (targetDate.HasValue && (fromDate.HasValue || toDate.HasValue))
            {
                return Results.BadRequest(new { error = "Используйте либо date, либо from/to." });
            }

            var query = dbContext.CalendarTasks
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .Include(x => x.Attributes)
                .Include(x => x.Skills)
                .Include(x => x.Habit)
                .AsQueryable();

            if (targetDate.HasValue)
            {
                query = query.Where(x => x.Date == targetDate.Value);
            }
            else if (fromDate.HasValue && toDate.HasValue)
            {
                query = query.Where(x => x.Date >= fromDate.Value && x.Date <= toDate.Value);
            }

            var tasks = await query
                .OrderBy(x => x.Date)
                .ThenBy(x => x.StartTime ?? TimeOnly.MaxValue)
                .ThenBy(x => x.Title)
                .ToListAsync(cancellationToken);

            return Results.Ok(tasks.Select(MapTaskResponse).ToList());
        }

        private static async Task<IResult> CreateTaskAsync(
            CreateCalendarTaskRequest request,
            ClaimsPrincipal principal,
            AppDbContext dbContext,
            ICurrentUserProvider currentUserProvider,
            CancellationToken cancellationToken)
        {
            var userId = currentUserProvider.GetUserId(principal);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var title = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(title) || title.Length is < 2 or > 120)
            {
                return Results.BadRequest(new { error = "Название должно быть от 2 до 120 символов." });
            }

            if (!DateOnly.TryParse(request.Date, out var date))
            {
                return Results.BadRequest(new { error = "Неверная дата." });
            }

            var importance = NormalizeImportance(request.Importance);
            if (importance is null)
            {
                return Results.BadRequest(new { error = "Неверная важность." });
            }

            var canUseImportance = await ValidateImportanceLimitAsync(
                userId.Value,
                date,
                importance.Value,
                dbContext,
                cancellationToken);

            if (!canUseImportance)
            {
                return Results.BadRequest(new
                {
                    error = GetImportanceLimitErrorMessage(importance.Value)
                });
            }

            var difficulty = NormalizeDifficulty(request.Difficulty);
            if (difficulty is null)
            {
                return Results.BadRequest(new { error = "Неверная сложность." });
            }

            TimeOnly? startTime = null;
            TimeOnly? endTime = null;

            if (!string.IsNullOrWhiteSpace(request.StartTime) ||
                !string.IsNullOrWhiteSpace(request.EndTime))
            {
                if (string.IsNullOrWhiteSpace(request.StartTime) ||
                    string.IsNullOrWhiteSpace(request.EndTime))
                {
                    return Results.BadRequest(new { error = "Укажите оба значения времени." });
                }

                if (!TimeOnly.TryParse(request.StartTime, out var parsedStart) ||
                    !TimeOnly.TryParse(request.EndTime, out var parsedEnd))
                {
                    return Results.BadRequest(new { error = "Неверный формат времени." });
                }

                if (parsedStart >= parsedEnd)
                {
                    return Results.BadRequest(new { error = "Время окончания должно быть позже начала." });
                }

                startTime = parsedStart;
                endTime = parsedEnd;
            }

            var attributes = NormalizeAttributes(request.Attributes);
            var skillIds = await NormalizeSkillIdsAsync(
                request.SkillIds,
                userId.Value,
                dbContext,
                cancellationToken);

            if (skillIds is null)
            {
                return Results.BadRequest(new { error = "Некоторые навыки не найдены." });
            }

            var habit = await GetHabitIfBelongsToUserAsync(
                request.HabitId,
                userId.Value,
                dbContext,
                cancellationToken);

            if (request.HabitId.HasValue && habit is null)
            {
                return Results.BadRequest(new { error = "Привычка не найдена." });
            }

            var now = DateTime.UtcNow;
            var task = new CalendarTask
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Date = date,
                Title = title,
                Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
                Importance = importance.Value,
                Difficulty = difficulty.Value,
                IsCompleted = request.IsCompleted ?? false,
                CompletedAtUtc = request.IsCompleted == true ? now : null,
                HabitId = habit?.Id,
                StartTime = startTime,
                EndTime = endTime,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Attributes = attributes
                    .Select(attributeType => new CalendarTaskAttribute
                    {
                        CalendarTaskId = Guid.Empty,
                        AttributeType = attributeType
                    })
                    .ToList(),
                Skills = skillIds
                    .Select(skillId => new CalendarTaskSkill
                    {
                        CalendarTaskId = Guid.Empty,
                        UserSkillId = skillId
                    })
                    .ToList()
            };

            foreach (var attribute in task.Attributes)
            {
                attribute.CalendarTaskId = task.Id;
            }

            foreach (var skill in task.Skills)
            {
                skill.CalendarTaskId = task.Id;
            }

            dbContext.CalendarTasks.Add(task);

            if (task.IsCompleted)
            {
                var skillAttributesBySkillId = await LoadSkillAttributesMapAsync(
                    userId.Value,
                    task.Skills.Select(x => x.UserSkillId),
                    dbContext,
                    cancellationToken);

                var attributeDeltas = BuildAttributeDeltasForTaskCompletion(
                    task.Attributes.Select(x => x.AttributeType),
                    task.Skills.Select(x => x.UserSkillId),
                    task.Difficulty,
                    skillAttributesBySkillId);

                await ApplyAttributeDeltasAsync(
                    userId.Value,
                    attributeDeltas,
                    dbContext,
                    cancellationToken);

                if (task.HabitId.HasValue)
                {
                    await EnsureHabitCompletedAsync(
                        task.HabitId.Value,
                        task.Date,
                        userId.Value,
                        dbContext,
                        cancellationToken);
                }

                var skillUseDeltas = new Dictionary<Guid, int>();
                AddSkillUseDeltas(
                    skillUseDeltas,
                    task.Skills.Select(x => x.UserSkillId),
                    GetSkillUsesForDifficulty(task.Difficulty));

                await ApplySkillUsesDeltaAsync(
                    userId.Value,
                    skillUseDeltas,
                    dbContext,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var recalculateResult = await RecalculateTaskExperienceForDateAsync(
                userId.Value,
                task.Date,
                dbContext,
                cancellationToken);

            if (recalculateResult is not null)
            {
                return recalculateResult;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(MapTaskResponse(task));
        }

        private static async Task<IResult> UpdateTaskAsync(
            Guid id,
            UpdateCalendarTaskRequest request,
            ClaimsPrincipal principal,
            AppDbContext dbContext,
            ICurrentUserProvider currentUserProvider,
            CancellationToken cancellationToken)
        {
            var userId = currentUserProvider.GetUserId(principal);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var task = await dbContext.CalendarTasks
                .Include(x => x.Attributes)
                .Include(x => x.Skills)
                .Include(x => x.Habit)
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, cancellationToken);

            if (task is null)
            {
                return Results.NotFound(new { error = "Дело не найдено." });
            }

            var title = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(title) || title.Length is < 2 or > 120)
            {
                return Results.BadRequest(new { error = "Название должно быть от 2 до 120 символов." });
            }

            if (!DateOnly.TryParse(request.Date, out var date))
            {
                return Results.BadRequest(new { error = "Неверная дата." });
            }

            var importance = NormalizeImportance(request.Importance);
            if (importance is null)
            {
                return Results.BadRequest(new { error = "Неверная важность." });
            }

            var canUseImportance = await ValidateImportanceLimitAsync(
                userId.Value,
                date,
                importance.Value,
                dbContext,
                cancellationToken,
                excludeTaskId: task.Id);

            if (!canUseImportance)
            {
                return Results.BadRequest(new
                {
                    error = GetImportanceLimitErrorMessage(importance.Value)
                });
            }

            var difficulty = NormalizeDifficulty(request.Difficulty);
            if (difficulty is null)
            {
                return Results.BadRequest(new { error = "Неверная сложность." });
            }

            TimeOnly? startTime = null;
            TimeOnly? endTime = null;

            if (!string.IsNullOrWhiteSpace(request.StartTime) ||
                !string.IsNullOrWhiteSpace(request.EndTime))
            {
                if (string.IsNullOrWhiteSpace(request.StartTime) ||
                    string.IsNullOrWhiteSpace(request.EndTime))
                {
                    return Results.BadRequest(new { error = "Укажите оба значения времени." });
                }

                if (!TimeOnly.TryParse(request.StartTime, out var parsedStart) ||
                    !TimeOnly.TryParse(request.EndTime, out var parsedEnd))
                {
                    return Results.BadRequest(new { error = "Неверный формат времени." });
                }

                if (parsedStart >= parsedEnd)
                {
                    return Results.BadRequest(new { error = "Время окончания должно быть позже начала." });
                }

                startTime = parsedStart;
                endTime = parsedEnd;
            }

            var attributes = NormalizeAttributes(request.Attributes);
            var skillIds = await NormalizeSkillIdsAsync(
                request.SkillIds,
                userId.Value,
                dbContext,
                cancellationToken);

            if (skillIds is null)
            {
                return Results.BadRequest(new { error = "Некоторые навыки не найдены." });
            }

            var habit = await GetHabitIfBelongsToUserAsync(
                request.HabitId,
                userId.Value,
                dbContext,
                cancellationToken);

            if (request.HabitId.HasValue && habit is null)
            {
                return Results.BadRequest(new { error = "Привычка не найдена." });
            }

            var previousDate = task.Date;
            var wasCompleted = task.IsCompleted;
            var previousDifficulty = task.Difficulty;
            var previousHabitId = task.HabitId;
            var previousTaskAttributes = task.Attributes
                .Select(x => x.AttributeType)
                .Distinct()
                .ToList();
            var previousSkillIds = task.Skills
                .Select(x => x.UserSkillId)
                .Distinct()
                .ToList();

            var nextIsCompleted = request.IsCompleted ?? task.IsCompleted;
            var now = DateTime.UtcNow;

            task.Title = title;
            task.Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim();
            task.Date = date;
            task.Importance = importance.Value;
            task.Difficulty = difficulty.Value;
            task.IsCompleted = nextIsCompleted;
            task.HabitId = habit?.Id;
            task.StartTime = startTime;
            task.EndTime = endTime;
            task.UpdatedAtUtc = now;

            if (!wasCompleted && task.IsCompleted)
            {
                task.CompletedAtUtc = now;
            }
            else if (wasCompleted && !task.IsCompleted)
            {
                task.CompletedAtUtc = null;
            }
            else if (task.IsCompleted && task.CompletedAtUtc is null)
            {
                task.CompletedAtUtc = now;
            }

            dbContext.CalendarTaskAttributes.RemoveRange(task.Attributes);
            task.Attributes = attributes
                .Select(attributeType => new CalendarTaskAttribute
                {
                    CalendarTaskId = task.Id,
                    AttributeType = attributeType
                })
                .ToList();

            dbContext.CalendarTaskSkills.RemoveRange(task.Skills);
            task.Skills = skillIds
                .Select(skillId => new CalendarTaskSkill
                {
                    CalendarTaskId = task.Id,
                    UserSkillId = skillId
                })
                .ToList();

            var skillAttributesBySkillId = await LoadSkillAttributesMapAsync(
                userId.Value,
                previousSkillIds.Concat(task.Skills.Select(x => x.UserSkillId)),
                dbContext,
                cancellationToken);

            var attributeDeltas = new Dictionary<AttributeType, int>();
            if (wasCompleted)
            {
                AddAttributeDeltas(
                    attributeDeltas,
                    BuildAttributeDeltasForTaskCompletion(
                        previousTaskAttributes,
                        previousSkillIds,
                        previousDifficulty,
                        skillAttributesBySkillId),
                    -1);
            }

            if (task.IsCompleted)
            {
                AddAttributeDeltas(
                    attributeDeltas,
                    BuildAttributeDeltasForTaskCompletion(
                        task.Attributes.Select(x => x.AttributeType),
                        task.Skills.Select(x => x.UserSkillId),
                        task.Difficulty,
                        skillAttributesBySkillId),
                    1);
            }

            await ApplyAttributeDeltasAsync(
                userId.Value,
                attributeDeltas,
                dbContext,
                cancellationToken);

            var skillUseDeltas = new Dictionary<Guid, int>();
            if (wasCompleted)
            {
                AddSkillUseDeltas(
                    skillUseDeltas,
                    previousSkillIds,
                    -GetSkillUsesForDifficulty(previousDifficulty));
            }

            if (task.IsCompleted)
            {
                AddSkillUseDeltas(
                    skillUseDeltas,
                    task.Skills.Select(x => x.UserSkillId),
                    GetSkillUsesForDifficulty(task.Difficulty));
            }

            await ApplySkillUsesDeltaAsync(
                userId.Value,
                skillUseDeltas,
                dbContext,
                cancellationToken);

            var becameCompleted = !wasCompleted && task.IsCompleted;
            var completedTaskChangedLinkedHabit =
                task.IsCompleted &&
                (previousHabitId != task.HabitId || previousDate != task.Date);

            if ((becameCompleted || completedTaskChangedLinkedHabit) && task.HabitId.HasValue)
            {
                await EnsureHabitCompletedAsync(
                    task.HabitId.Value,
                    task.Date,
                    userId.Value,
                    dbContext,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var affectedDates = new HashSet<DateOnly> { task.Date, previousDate };
            foreach (var affectedDate in affectedDates)
            {
                var recalculateResult = await RecalculateTaskExperienceForDateAsync(
                    userId.Value,
                    affectedDate,
                    dbContext,
                    cancellationToken);

                if (recalculateResult is not null)
                {
                    return recalculateResult;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(MapTaskResponse(task));
        }

        private static async Task<IResult> DeleteTaskAsync(
            Guid id,
            ClaimsPrincipal principal,
            AppDbContext dbContext,
            ICurrentUserProvider currentUserProvider,
            CancellationToken cancellationToken)
        {
            var userId = currentUserProvider.GetUserId(principal);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var task = await dbContext.CalendarTasks
                .Include(x => x.Attributes)
                .Include(x => x.Skills)
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, cancellationToken);

            if (task is null)
            {
                return Results.NotFound(new { error = "Дело не найдено." });
            }

            var deletedDate = task.Date;
            var deletedExperience = task.ExperienceAwarded;

            if (task.IsCompleted)
            {
                var skillAttributesBySkillId = await LoadSkillAttributesMapAsync(
                    userId.Value,
                    task.Skills.Select(x => x.UserSkillId),
                    dbContext,
                    cancellationToken);

                var attributeDeltas = BuildAttributeDeltasForTaskCompletion(
                    task.Attributes.Select(x => x.AttributeType),
                    task.Skills.Select(x => x.UserSkillId),
                    task.Difficulty,
                    skillAttributesBySkillId);

                await ApplyAttributeDeltasAsync(
                    userId.Value,
                    attributeDeltas.ToDictionary(x => x.Key, x => -x.Value),
                    dbContext,
                    cancellationToken);

                var skillUseDeltas = new Dictionary<Guid, int>();
                AddSkillUseDeltas(
                    skillUseDeltas,
                    task.Skills.Select(x => x.UserSkillId),
                    -GetSkillUsesForDifficulty(task.Difficulty));

                await ApplySkillUsesDeltaAsync(
                    userId.Value,
                    skillUseDeltas,
                    dbContext,
                    cancellationToken);
            }

            dbContext.CalendarTasks.Remove(task);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (deletedExperience != 0)
            {
                var profileUpdated = await ApplyTaskExperienceAsync(
                    userId.Value,
                    -deletedExperience,
                    dbContext,
                    cancellationToken);

                if (!profileUpdated)
                {
                    return Results.NotFound(new { error = "Профиль персонажа не найден." });
                }
            }

            var recalculateResult = await RecalculateTaskExperienceForDateAsync(
                userId.Value,
                deletedDate,
                dbContext,
                cancellationToken);

            if (recalculateResult is not null)
            {
                return recalculateResult;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }

        private static CalendarTaskResponse MapTaskResponse(CalendarTask task)
        {
            return new CalendarTaskResponse(
                task.Id,
                task.Date.ToString("yyyy-MM-dd"),
                task.Title,
                task.Details,
                task.Importance,
                task.Difficulty,
                task.IsCompleted,
                task.StartTime?.ToString("HH:mm"),
                task.EndTime?.ToString("HH:mm"),
                task.Attributes
                    .OrderBy(x => x.AttributeType)
                    .Select(x => x.AttributeType.ToString())
                    .ToList(),
                task.Skills
                    .OrderBy(x => x.UserSkillId)
                    .Select(x => x.UserSkillId)
                    .ToList(),
                task.HabitId,
                task.Habit?.Name,
                task.ExperienceAwarded,
                task.IsFirstTaskBonusApplied);
        }

        private static async Task<IResult?> RecalculateTaskExperienceForDateAsync(
            Guid userId,
            DateOnly date,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var tasks = await dbContext.CalendarTasks
                .Where(x => x.UserId == userId && x.Date == date)
                .OrderBy(x => x.CompletedAtUtc ?? DateTime.MaxValue)
                .ThenBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            if (tasks.Count == 0)
            {
                return null;
            }

            var currentTotal = tasks.Sum(x => x.ExperienceAwarded);
            var expectedTotal = 0;

            var completedTasks = tasks
                .Where(x => x.IsCompleted)
                .OrderBy(x => x.CompletedAtUtc ?? x.UpdatedAtUtc)
                .ThenBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList();

            for (var index = 0; index < completedTasks.Count; index++)
            {
                var completedTask = completedTasks[index];
                var isFirstTaskOfDay = index == 0;
                var expectedExperience = CalculateTaskExperience(
                    completedTask.Importance,
                    completedTask.Difficulty,
                    isFirstTaskOfDay);

                completedTask.ExperienceAwarded = expectedExperience;
                completedTask.IsFirstTaskBonusApplied = isFirstTaskOfDay;
                completedTask.CompletedAtUtc ??= completedTask.UpdatedAtUtc;
                expectedTotal += expectedExperience;
            }

            foreach (var task in tasks.Where(x => !x.IsCompleted))
            {
                task.ExperienceAwarded = 0;
                task.IsFirstTaskBonusApplied = false;
                task.CompletedAtUtc = null;
            }

            var experienceDelta = expectedTotal - currentTotal;
            if (experienceDelta == 0)
            {
                return null;
            }

            var profileUpdated = await ApplyTaskExperienceAsync(
                userId,
                experienceDelta,
                dbContext,
                cancellationToken);

            if (!profileUpdated)
            {
                return Results.NotFound(new { error = "Профиль персонажа не найден." });
            }

            return null;
        }

        private static async Task<bool> ApplyTaskExperienceAsync(
            Guid userId,
            int experienceDelta,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            if (experienceDelta == 0)
            {
                return true;
            }

            var profile = await dbContext.CharacterProfiles
                .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (profile is null)
            {
                return false;
            }

            CharacterExperience.ApplyExperience(profile, experienceDelta);
            return true;
        }

        private static int CalculateTaskExperience(
            TaskImportance importance,
            Difficulty difficulty,
            bool isFirstTaskOfDay)
        {
            var baseExperience = GetBaseExperienceForImportance(importance);
            var multiplier = GetDifficultyMultiplier(difficulty);
            var total = baseExperience * multiplier;

            if (isFirstTaskOfDay)
            {
                total *= 2;
            }

            return total;
        }

        private static int GetBaseExperienceForImportance(TaskImportance importance)
        {
            return importance switch
            {
                TaskImportance.Required => 40,
                TaskImportance.Important => 25,
                TaskImportance.Optional => 15,
                _ => 15
            };
        }

        private static int GetDifficultyMultiplier(Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy => 1,
                Difficulty.Medium => 2,
                Difficulty.Hard => 3,
                _ => 2
            };
        }

        private static int GetTaskAttributeGainForDifficulty(Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy => 1,
                Difficulty.Medium => 2,
                Difficulty.Hard => 3,
                _ => 2
            };
        }

        private static int GetSkillAttributeGainForDifficulty(Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy => 1,
                Difficulty.Medium => 1,
                Difficulty.Hard => 2,
                _ => 1
            };
        }

        private static Dictionary<AttributeType, int> BuildAttributeDeltasForTaskCompletion(
            IEnumerable<AttributeType> taskAttributes,
            IEnumerable<Guid> skillIds,
            Difficulty difficulty,
            IReadOnlyDictionary<Guid, IReadOnlyList<SkillAttributeShare>> skillAttributesBySkillId)
        {
            var deltas = new Dictionary<AttributeType, int>();

            var taskAttributeGain = GetTaskAttributeGainForDifficulty(difficulty);
            foreach (var attributeType in taskAttributes.Distinct())
            {
                AddAttributeDelta(deltas, attributeType, taskAttributeGain);
            }

            var skillAttributeGain = GetSkillAttributeGainForDifficulty(difficulty);
            foreach (var skillId in skillIds.Distinct())
            {
                if (!skillAttributesBySkillId.TryGetValue(skillId, out var relatedAttributes))
                {
                    continue;
                }

                var distributedDeltas = DistributeSkillAttributeGain(
                    relatedAttributes,
                    skillAttributeGain);

                foreach (var (attributeType, delta) in distributedDeltas)
                {
                    AddAttributeDelta(deltas, attributeType, delta);
                }
            }

            return deltas;
        }

        private static Dictionary<AttributeType, int> DistributeSkillAttributeGain(
            IReadOnlyList<SkillAttributeShare> attributeShares,
            int totalGain)
        {
            var result = new Dictionary<AttributeType, int>();
            if (totalGain <= 0 || attributeShares.Count == 0)
            {
                return result;
            }

            var normalizedShares = attributeShares
                .GroupBy(x => x.AttributeType)
                .Select(group => new SkillAttributeShare(
                    group.Key,
                    group.Sum(x => Math.Max(0, x.SharePercent))))
                .Where(x => x.SharePercent > 0)
                .ToList();

            if (normalizedShares.Count == 0)
            {
                return result;
            }

            var totalPercent = normalizedShares.Sum(x => x.SharePercent);
            if (totalPercent <= 0)
            {
                return result;
            }

            var allocations = normalizedShares
                .Select(x =>
                {
                    var weighted = totalGain * (double)x.SharePercent / totalPercent;
                    var baseValue = (int)Math.Floor(weighted);
                    return new
                    {
                        x.AttributeType,
                        BaseValue = baseValue,
                        Fraction = weighted - baseValue,
                        Percent = x.SharePercent
                    };
                })
                .ToList();

            var allocated = allocations.Sum(x => x.BaseValue);
            var remaining = totalGain - allocated;

            foreach (var allocation in allocations)
            {
                if (allocation.BaseValue > 0)
                {
                    AddAttributeDelta(result, allocation.AttributeType, allocation.BaseValue);
                }
            }

            if (remaining <= 0)
            {
                return result;
            }

            foreach (var allocation in allocations
                .OrderByDescending(x => x.Fraction)
                .ThenByDescending(x => x.Percent)
                .ThenBy(x => x.AttributeType)
                .Take(remaining))
            {
                AddAttributeDelta(result, allocation.AttributeType, 1);
            }

            return result;
        }

        private static void AddAttributeDeltas(
            IDictionary<AttributeType, int> target,
            IReadOnlyDictionary<AttributeType, int> source,
            int sign)
        {
            if (sign == 0 || source.Count == 0)
            {
                return;
            }

            foreach (var (attributeType, delta) in source)
            {
                AddAttributeDelta(target, attributeType, delta * sign);
            }
        }

        private static void AddAttributeDelta(
            IDictionary<AttributeType, int> target,
            AttributeType attributeType,
            int delta)
        {
            if (delta == 0)
            {
                return;
            }

            if (target.TryGetValue(attributeType, out var existing))
            {
                target[attributeType] = existing + delta;
                return;
            }

            target[attributeType] = delta;
        }

        private static async Task<Dictionary<Guid, IReadOnlyList<SkillAttributeShare>>> LoadSkillAttributesMapAsync(
            Guid userId,
            IEnumerable<Guid> skillIds,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var distinctSkillIds = skillIds
                .Distinct()
                .ToList();

            if (distinctSkillIds.Count == 0)
            {
                return new Dictionary<Guid, IReadOnlyList<SkillAttributeShare>>();
            }

            var rows = await dbContext.UserSkillAttributes
                .AsNoTracking()
                .Where(x =>
                    distinctSkillIds.Contains(x.UserSkillId) &&
                    x.UserSkill.UserId == userId)
                .Select(x => new
                {
                    x.UserSkillId,
                    x.AttributeType,
                    x.SharePercent
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(x => x.UserSkillId)
                .ToDictionary(
                    x => x.Key,
                    x => (IReadOnlyList<SkillAttributeShare>)x
                        .GroupBy(v => v.AttributeType)
                        .Select(group => new SkillAttributeShare(
                            group.Key,
                            group.Sum(item => item.SharePercent)))
                        .OrderBy(v => v.AttributeType)
                        .ToList());
        }

        private static async Task ApplyAttributeDeltasAsync(
            Guid userId,
            IReadOnlyDictionary<AttributeType, int> attributeDeltas,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var effectiveAttributeTypes = attributeDeltas
                .Where(x => x.Value != 0)
                .Select(x => x.Key)
                .Distinct()
                .ToList();

            if (effectiveAttributeTypes.Count == 0)
            {
                return;
            }

            var attributes = await dbContext.CharacterAttributes
                .Where(x =>
                    x.Profile.UserId == userId &&
                    effectiveAttributeTypes.Contains(x.AttributeType))
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var attribute in attributes)
            {
                if (!attributeDeltas.TryGetValue(attribute.AttributeType, out var delta) || delta == 0)
                {
                    continue;
                }

                attribute.Value = Math.Max(0, attribute.Value + delta);
                attribute.UpdatedAtUtc = now;
            }
        }

        private static int GetSkillUsesForDifficulty(Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy => 1,
                Difficulty.Medium => 2,
                Difficulty.Hard => 3,
                _ => 2
            };
        }

        private static void AddSkillUseDeltas(
            IDictionary<Guid, int> target,
            IEnumerable<Guid> skillIds,
            int deltaPerSkill)
        {
            if (deltaPerSkill == 0)
            {
                return;
            }

            foreach (var skillId in skillIds.Distinct())
            {
                if (target.TryGetValue(skillId, out var existing))
                {
                    target[skillId] = existing + deltaPerSkill;
                    continue;
                }

                target[skillId] = deltaPerSkill;
            }
        }

        private static async Task ApplySkillUsesDeltaAsync(
            Guid userId,
            IDictionary<Guid, int> skillUseDeltas,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var effectiveSkillIds = skillUseDeltas
                .Where(x => x.Value != 0)
                .Select(x => x.Key)
                .Distinct()
                .ToList();

            if (effectiveSkillIds.Count == 0)
            {
                return;
            }

            var userSkills = await dbContext.UserSkills
                .Where(x => x.UserId == userId && effectiveSkillIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var userSkill in userSkills)
            {
                if (!skillUseDeltas.TryGetValue(userSkill.Id, out var delta) || delta == 0)
                {
                    continue;
                }

                ApplySkillUsesDelta(userSkill, delta);
            }
        }

        private static void ApplySkillUsesDelta(UserSkill userSkill, int delta)
        {
            userSkill.Level = Math.Max(1, userSkill.Level);
            userSkill.RequiredUsesForNextLevel = CalculateRequiredUsesForLevel(userSkill.Level);
            userSkill.CurrentUses += delta;

            while (userSkill.CurrentUses >= userSkill.RequiredUsesForNextLevel)
            {
                userSkill.CurrentUses -= userSkill.RequiredUsesForNextLevel;
                userSkill.Level += 1;
                userSkill.RequiredUsesForNextLevel = CalculateRequiredUsesForLevel(userSkill.Level);
            }

            while (userSkill.CurrentUses < 0 && userSkill.Level > 1)
            {
                userSkill.Level -= 1;
                userSkill.RequiredUsesForNextLevel = CalculateRequiredUsesForLevel(userSkill.Level);
                userSkill.CurrentUses += userSkill.RequiredUsesForNextLevel;
            }

            if (userSkill.CurrentUses < 0)
            {
                userSkill.CurrentUses = 0;
            }

            userSkill.RequiredUsesForNextLevel = CalculateRequiredUsesForLevel(userSkill.Level);
            userSkill.UpdatedAtUtc = DateTime.UtcNow;
        }

        private static int CalculateRequiredUsesForLevel(int level)
        {
            var normalizedLevel = Math.Max(1, level);
            return 5 + normalizedLevel * 2;
        }

        private static IReadOnlyList<AttributeType> NormalizeAttributes(
            IReadOnlyList<AttributeType>? attributes)
        {
            if (attributes is null || attributes.Count == 0)
            {
                return Array.Empty<AttributeType>();
            }

            return attributes
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        private static Difficulty? NormalizeDifficulty(Difficulty difficulty)
        {
            if (difficulty == Difficulty.Unknown)
            {
                return Difficulty.Medium;
            }

            if (!Enum.IsDefined(typeof(Difficulty), difficulty))
            {
                return null;
            }

            return difficulty;
        }

        private static TaskImportance? NormalizeImportance(TaskImportance importance)
        {
            if (importance == TaskImportance.Unknown)
            {
                return TaskImportance.Optional;
            }

            if (!Enum.IsDefined(typeof(TaskImportance), importance))
            {
                return null;
            }

            return importance;
        }

        private static int? GetImportanceLimit(TaskImportance importance)
        {
            return importance switch
            {
                TaskImportance.Required => 1,
                TaskImportance.Important => 3,
                TaskImportance.Optional => null,
                _ => null
            };
        }

        private static string GetImportanceLimitErrorMessage(TaskImportance importance)
        {
            return importance switch
            {
                TaskImportance.Required => "На день можно добавить только 1 обязательную задачу.",
                TaskImportance.Important => "На день можно добавить максимум 3 важные задачи.",
                _ => "Для этой важности ограничений нет."
            };
        }

        private static async Task<bool> ValidateImportanceLimitAsync(
            Guid userId,
            DateOnly date,
            TaskImportance importance,
            AppDbContext dbContext,
            CancellationToken cancellationToken,
            Guid? excludeTaskId = null)
        {
            var limit = GetImportanceLimit(importance);
            if (!limit.HasValue)
            {
                return true;
            }

            var query = dbContext.CalendarTasks
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.Date == date &&
                    x.Importance == importance);

            if (excludeTaskId.HasValue)
            {
                query = query.Where(x => x.Id != excludeTaskId.Value);
            }

            var currentCount = await query.CountAsync(cancellationToken);
            return currentCount < limit.Value;
        }

        private static async Task<IReadOnlyList<Guid>?> NormalizeSkillIdsAsync(
            IReadOnlyList<Guid>? skillIds,
            Guid userId,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            if (skillIds is null || skillIds.Count == 0)
            {
                return Array.Empty<Guid>();
            }

            var distinctIds = skillIds.Distinct().ToList();

            var existing = await dbContext.UserSkills
                .AsNoTracking()
                .Where(x => x.UserId == userId && distinctIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (existing.Count != distinctIds.Count)
            {
                return null;
            }

            return existing.OrderBy(x => x).ToList();
        }

        private static async Task<Habit?> GetHabitIfBelongsToUserAsync(
            Guid? habitId,
            Guid userId,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            if (!habitId.HasValue)
            {
                return null;
            }

            return await dbContext.Habits
                .SingleOrDefaultAsync(
                    x => x.Id == habitId.Value && x.UserId == userId,
                    cancellationToken);
        }

        private static async Task EnsureHabitCompletedAsync(
            Guid habitId,
            DateOnly date,
            Guid userId,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var completionExists = await dbContext.HabitCompletions
                .AnyAsync(x => x.HabitId == habitId && x.Date == date, cancellationToken);

            if (completionExists)
            {
                return;
            }

            dbContext.HabitCompletions.Add(new HabitCompletion
            {
                HabitId = habitId,
                Date = date,
                CompletedAtUtc = DateTime.UtcNow
            });

            var discipline = await dbContext.CharacterAttributes
                .SingleOrDefaultAsync(
                    x => x.Profile.UserId == userId && x.AttributeType == AttributeType.Discipline,
                    cancellationToken);

            if (discipline is not null)
            {
                discipline.Value += 1;
                discipline.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        private sealed record SkillAttributeShare(
            AttributeType AttributeType,
            int SharePercent);

        public sealed record CreateCalendarTaskRequest(
            string Title,
            string Date,
            string? Details,
            TaskImportance Importance,
            Difficulty Difficulty,
            bool? IsCompleted,
            string? StartTime,
            string? EndTime,
            IReadOnlyList<AttributeType>? Attributes,
            IReadOnlyList<Guid>? SkillIds,
            Guid? HabitId);

        public sealed record UpdateCalendarTaskRequest(
            string Title,
            string Date,
            string? Details,
            TaskImportance Importance,
            Difficulty Difficulty,
            bool? IsCompleted,
            string? StartTime,
            string? EndTime,
            IReadOnlyList<AttributeType>? Attributes,
            IReadOnlyList<Guid>? SkillIds,
            Guid? HabitId);

        public sealed record CalendarTaskResponse(
            Guid Id,
            string Date,
            string Title,
            string? Details,
            TaskImportance Importance,
            Difficulty Difficulty,
            bool IsCompleted,
            string? StartTime,
            string? EndTime,
            IReadOnlyList<string> Attributes,
            IReadOnlyList<Guid> SkillIds,
            Guid? HabitId,
            string? HabitName,
            int ExperienceAwarded,
            bool IsFirstTaskBonusApplied);
    }
}
