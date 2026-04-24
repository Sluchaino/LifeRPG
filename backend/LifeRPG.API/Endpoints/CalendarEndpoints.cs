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
                return Results.BadRequest(new { error = "Habit was not found." });
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

                if (task.HabitId.HasValue)
                {
                    await EnsureHabitCompletedAsync(
                        task.HabitId.Value,
                        task.Date,
                        userId.Value,
                        dbContext,
                        cancellationToken);
                }
            }

            dbContext.CalendarTasks.Add(task);
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
                return Results.BadRequest(new { error = "Habit was not found." });
            }

            var previousDifficulty = task.Difficulty;
            var wasCompleted = task.IsCompleted;
            var previousDate = task.Date;
            var previousHabitId = task.HabitId;

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
