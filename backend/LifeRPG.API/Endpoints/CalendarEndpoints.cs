using System.Security.Claims;
using Domain.Progression;

namespace LifeRPG.API.Endpoints
{
    public static class CalendarEndpoints
    {
        public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/calendar").RequireAuthorization();

            group.MapGet("/tasks", GetTasksAsync);
            group.MapGet("/tasks/range", GetTasksRangeAsync);
            group.MapPost("/tasks", CreateTaskAsync);
            group.MapPatch("/tasks/{id:guid}", UpdateTaskAsync);
            group.MapDelete("/tasks/{id:guid}", DeleteTaskAsync);

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
                .Select(x => new CalendarTaskResponse(
                    x.Id,
                    x.Date.ToString("yyyy-MM-dd"),
                    x.Title,
                    x.Details,
                    x.Difficulty,
                    x.IsCompleted,
                    x.StartTime.HasValue ? x.StartTime.Value.ToString("HH:mm") : null,
                    x.EndTime.HasValue ? x.EndTime.Value.ToString("HH:mm") : null,
                    x.Attributes
                        .OrderBy(a => a.AttributeType)
                        .Select(a => a.AttributeType.ToString())
                        .ToList(),
                    x.Skills
                        .OrderBy(s => s.UserSkillId)
                        .Select(s => s.UserSkillId)
                        .ToList(),
                    x.HabitId,
                    x.Habit != null ? x.Habit.Name : null))
                .ToListAsync(cancellationToken);

            return Results.Ok(tasks);
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
                .Select(x => new CalendarTaskResponse(
                    x.Id,
                    x.Date.ToString("yyyy-MM-dd"),
                    x.Title,
                    x.Details,
                    x.Difficulty,
                    x.IsCompleted,
                    x.StartTime.HasValue ? x.StartTime.Value.ToString("HH:mm") : null,
                    x.EndTime.HasValue ? x.EndTime.Value.ToString("HH:mm") : null,
                    x.Attributes
                        .OrderBy(a => a.AttributeType)
                        .Select(a => a.AttributeType.ToString())
                        .ToList(),
                    x.Skills
                        .OrderBy(s => s.UserSkillId)
                        .Select(s => s.UserSkillId)
                        .ToList(),
                    x.HabitId,
                    x.Habit != null ? x.Habit.Name : null))
                .ToListAsync(cancellationToken);

            return Results.Ok(tasks);
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
                return Results.BadRequest(new { error = "\u041f\u0440\u0438\u0432\u044b\u0447\u043a\u0430 \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d\u0430." });
            }

            var now = DateTime.UtcNow;

            var task = new CalendarTask
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Date = date,
                Title = title,
                Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
                Difficulty = difficulty.Value,
                IsCompleted = request.IsCompleted ?? false,
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
                var profileUpdated = await ApplyTaskExperienceAsync(
                    userId.Value,
                    CharacterExperience.GetTaskExperience(task.Difficulty),
                    dbContext,
                    cancellationToken);

                if (!profileUpdated)
                {
                    return Results.NotFound(new { error = "Профиль персонажа не найден." });
                }

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

            var response = new CalendarTaskResponse(
                task.Id,
                task.Date.ToString("yyyy-MM-dd"),
                task.Title,
                task.Details,
                task.Difficulty,
                task.IsCompleted,
                task.StartTime.HasValue ? task.StartTime.Value.ToString("HH:mm") : null,
                task.EndTime.HasValue ? task.EndTime.Value.ToString("HH:mm") : null,
                task.Attributes
                    .OrderBy(a => a.AttributeType)
                    .Select(a => a.AttributeType.ToString())
                    .ToList(),
                task.Skills
                    .OrderBy(s => s.UserSkillId)
                    .Select(s => s.UserSkillId)
                    .ToList(),
                task.HabitId,
                habit?.Name);

            return Results.Ok(response);
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
                return Results.BadRequest(new { error = "\u041f\u0440\u0438\u0432\u044b\u0447\u043a\u0430 \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d\u0430." });
            }

            var previousDifficulty = task.Difficulty;
            var wasCompleted = task.IsCompleted;
            var previousDate = task.Date;
            var previousHabitId = task.HabitId;
            var previousTaskAttributes = task.Attributes
                .Select(x => x.AttributeType)
                .Distinct()
                .ToList();
            var previousSkillIds = task.Skills
                .Select(x => x.UserSkillId)
                .Distinct()
                .ToList();

            task.Title = title;
            task.Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim();
            task.Date = date;
            task.Difficulty = difficulty.Value;
            task.IsCompleted = request.IsCompleted ?? task.IsCompleted;
            task.HabitId = habit?.Id;
            task.StartTime = startTime;
            task.EndTime = endTime;
            task.UpdatedAtUtc = DateTime.UtcNow;

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

            var experienceDelta = CalculateTaskCompletionExperienceDelta(
                wasCompleted,
                previousDifficulty,
                task.IsCompleted,
                task.Difficulty);

            if (experienceDelta != 0)
            {
                var profileUpdated = await ApplyTaskExperienceAsync(
                    userId.Value,
                    experienceDelta,
                    dbContext,
                    cancellationToken);

                if (!profileUpdated)
                {
                    return Results.NotFound(new { error = "Профиль персонажа не найден." });
                }
            }

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

            var response = new CalendarTaskResponse(
                task.Id,
                task.Date.ToString("yyyy-MM-dd"),
                task.Title,
                task.Details,
                task.Difficulty,
                task.IsCompleted,
                task.StartTime.HasValue ? task.StartTime.Value.ToString("HH:mm") : null,
                task.EndTime.HasValue ? task.EndTime.Value.ToString("HH:mm") : null,
                task.Attributes
                    .OrderBy(a => a.AttributeType)
                    .Select(a => a.AttributeType.ToString())
                    .ToList(),
                task.Skills
                    .OrderBy(s => s.UserSkillId)
                    .Select(s => s.UserSkillId)
                    .ToList(),
                task.HabitId,
                habit?.Name);

            return Results.Ok(response);
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

            if (task.IsCompleted)
            {
                var profileUpdated = await ApplyTaskExperienceAsync(
                    userId.Value,
                    -CharacterExperience.GetTaskExperience(task.Difficulty),
                    dbContext,
                    cancellationToken);

                if (!profileUpdated)
                {
                    return Results.NotFound(new { error = "Профиль персонажа не найден." });
                }
            }

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

            return Results.NoContent();
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

        private static int CalculateTaskCompletionExperienceDelta(
            bool wasCompleted,
            Difficulty previousDifficulty,
            bool isCompleted,
            Difficulty currentDifficulty)
        {
            if (!wasCompleted && isCompleted)
            {
                return CharacterExperience.GetTaskExperience(currentDifficulty);
            }

            if (wasCompleted && !isCompleted)
            {
                return -CharacterExperience.GetTaskExperience(previousDifficulty);
            }

            if (wasCompleted && isCompleted && previousDifficulty != currentDifficulty)
            {
                return CharacterExperience.GetTaskExperience(currentDifficulty) -
                       CharacterExperience.GetTaskExperience(previousDifficulty);
            }

            return 0;
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
            IReadOnlyDictionary<Guid, IReadOnlyList<AttributeType>> skillAttributesBySkillId)
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

                foreach (var attributeType in relatedAttributes.Distinct())
                {
                    AddAttributeDelta(deltas, attributeType, skillAttributeGain);
                }
            }

            return deltas;
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

        private static async Task<Dictionary<Guid, IReadOnlyList<AttributeType>>> LoadSkillAttributesMapAsync(
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
                return new Dictionary<Guid, IReadOnlyList<AttributeType>>();
            }

            var rows = await dbContext.UserSkillAttributes
                .AsNoTracking()
                .Where(x =>
                    distinctSkillIds.Contains(x.UserSkillId) &&
                    x.UserSkill.UserId == userId)
                .Select(x => new { x.UserSkillId, x.AttributeType })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(x => x.UserSkillId)
                .ToDictionary(
                    x => x.Key,
                    x => (IReadOnlyList<AttributeType>)x
                        .Select(v => v.AttributeType)
                        .Distinct()
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

        public sealed record CreateCalendarTaskRequest(
            string Title,
            string Date,
            string? Details,
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
            Difficulty Difficulty,
            bool IsCompleted,
            string? StartTime,
            string? EndTime,
            IReadOnlyList<string> Attributes,
            IReadOnlyList<Guid> SkillIds,
            Guid? HabitId,
            string? HabitName);
    }
}
