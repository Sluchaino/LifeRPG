using System.Security.Claims;

namespace LifeRPG.API.Endpoints
{
    public static class HabitEndpoints
    {
        public static IEndpointRouteBuilder MapHabitEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/habits").RequireAuthorization();

            group.MapGet("/", GetHabitsAsync);
            group.MapPost("/", CreateHabitAsync);
            group.MapPatch("/{habitId:guid}", UpdateHabitAsync);
            group.MapDelete("/{habitId:guid}", DeleteHabitAsync);
            group.MapPost("/{habitId:guid}/toggle-completion", ToggleCompletionAsync);

            return app;
        }

        private static async Task<IResult> GetHabitsAsync(
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

            var today = GetCurrentLocalDate();

            var habits = await dbContext.Habits
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .Include(x => x.Completions)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var disciplineValue = await dbContext.CharacterAttributes
                .AsNoTracking()
                .Where(x => x.Profile.UserId == userId.Value && x.AttributeType == AttributeType.Discipline)
                .Select(x => x.Value)
                .SingleOrDefaultAsync(cancellationToken);

            var responseHabits = habits
                .Select(x =>
                {
                    var orderedCompletions = x.Completions
                        .OrderByDescending(c => c.Date)
                        .ToList();
                    var isCompletedToday = orderedCompletions.Any(c => c.Date == today);
                    var lastCompletedDate = orderedCompletions.FirstOrDefault()?.Date.ToString("yyyy-MM-dd");

                    return new HabitResponse(
                        x.Id,
                        x.Name,
                        x.Description,
                        isCompletedToday,
                        x.Completions.Count,
                        lastCompletedDate,
                        x.CreatedAtUtc,
                        x.UpdatedAtUtc);
                })
                .ToList();

            var completedToday = responseHabits.Count(x => x.IsCompletedToday);

            return Results.Ok(new HabitsResponse(
                today.ToString("yyyy-MM-dd"),
                disciplineValue,
                responseHabits.Count,
                completedToday,
                responseHabits));
        }

        private static async Task<IResult> CreateHabitAsync(
            CreateHabitRequest request,
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

            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length is < 2 or > 120)
            {
                return Results.BadRequest(new { error = "Название привычки должно быть от 2 до 120 символов." });
            }

            var normalizedName = NormalizeName(name);
            var duplicateExists = await dbContext.Habits
                .Where(x => x.UserId == userId.Value)
                .AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken);

            if (duplicateExists)
            {
                return Results.Conflict(new { error = "Привычка с таким названием уже существует." });
            }

            var now = DateTime.UtcNow;
            var habit = new Habit
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Name = name,
                NormalizedName = normalizedName,
                Description = NormalizeDescription(request.Description),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Habits.Add(habit);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new HabitResponse(
                habit.Id,
                habit.Name,
                habit.Description,
                false,
                0,
                null,
                habit.CreatedAtUtc,
                habit.UpdatedAtUtc));
        }

        private static async Task<IResult> UpdateHabitAsync(
            Guid habitId,
            UpdateHabitRequest request,
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

            var habit = await dbContext.Habits
                .Include(x => x.Completions)
                .SingleOrDefaultAsync(
                    x => x.Id == habitId && x.UserId == userId.Value,
                    cancellationToken);

            if (habit is null)
            {
                return Results.NotFound(new { error = "Привычка не найдена." });
            }

            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length is < 2 or > 120)
            {
                return Results.BadRequest(new { error = "Название привычки должно быть от 2 до 120 символов." });
            }

            var normalizedName = NormalizeName(name);
            var duplicateExists = await dbContext.Habits
                .Where(x => x.UserId == userId.Value && x.Id != habitId)
                .AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken);

            if (duplicateExists)
            {
                return Results.Conflict(new { error = "Привычка с таким названием уже существует." });
            }

            habit.Name = name;
            habit.NormalizedName = normalizedName;
            habit.Description = NormalizeDescription(request.Description);
            habit.UpdatedAtUtc = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            var lastCompletedDate = habit.Completions
                .OrderByDescending(x => x.Date)
                .Select(x => x.Date.ToString("yyyy-MM-dd"))
                .FirstOrDefault();

            var today = GetCurrentLocalDate();
            var isCompletedToday = habit.Completions.Any(x => x.Date == today);

            return Results.Ok(new HabitResponse(
                habit.Id,
                habit.Name,
                habit.Description,
                isCompletedToday,
                habit.Completions.Count,
                lastCompletedDate,
                habit.CreatedAtUtc,
                habit.UpdatedAtUtc));
        }

        private static async Task<IResult> DeleteHabitAsync(
            Guid habitId,
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

            var habit = await dbContext.Habits
                .SingleOrDefaultAsync(
                    x => x.Id == habitId && x.UserId == userId.Value,
                    cancellationToken);

            if (habit is null)
            {
                return Results.NotFound(new { error = "Привычка не найдена." });
            }

            dbContext.Habits.Remove(habit);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }

        private static async Task<IResult> ToggleCompletionAsync(
            Guid habitId,
            ToggleHabitCompletionRequest request,
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

            var habit = await dbContext.Habits
                .SingleOrDefaultAsync(
                    x => x.Id == habitId && x.UserId == userId.Value,
                    cancellationToken);

            if (habit is null)
            {
                return Results.NotFound(new { error = "Привычка не найдена." });
            }

            var today = GetCurrentLocalDate();
            var completionDate = today;
            if (!string.IsNullOrWhiteSpace(request.Date))
            {
                if (!DateOnly.TryParse(request.Date, out completionDate))
                {
                    return Results.BadRequest(new { error = "Неверный формат даты." });
                }

                if (completionDate != today)
                {
                    return Results.BadRequest(new { error = "Пока можно отмечать привычки только за текущий день." });
                }
            }

            var completion = await dbContext.HabitCompletions
                .SingleOrDefaultAsync(
                    x => x.HabitId == habitId && x.Date == completionDate,
                    cancellationToken);

            var shouldComplete = request.IsCompleted;
            var changed = false;

            if (shouldComplete && completion is null)
            {
                dbContext.HabitCompletions.Add(new HabitCompletion
                {
                    HabitId = habitId,
                    Date = completionDate,
                    CompletedAtUtc = DateTime.UtcNow
                });
                changed = true;
            }
            else if (!shouldComplete && completion is not null)
            {
                dbContext.HabitCompletions.Remove(completion);
                changed = true;
            }

            if (changed)
            {
                var discipline = await dbContext.CharacterAttributes
                    .SingleOrDefaultAsync(
                        x => x.Profile.UserId == userId.Value && x.AttributeType == AttributeType.Discipline,
                        cancellationToken);

                if (discipline is not null)
                {
                    discipline.Value = shouldComplete
                        ? Math.Round(discipline.Value + 1d, 2, MidpointRounding.AwayFromZero)
                        : Math.Max(0d, Math.Round(discipline.Value - 1d, 2, MidpointRounding.AwayFromZero));
                    discipline.UpdatedAtUtc = DateTime.UtcNow;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var disciplineValue = await dbContext.CharacterAttributes
                .AsNoTracking()
                .Where(x => x.Profile.UserId == userId.Value && x.AttributeType == AttributeType.Discipline)
                .Select(x => x.Value)
                .SingleOrDefaultAsync(cancellationToken);

            return Results.Ok(new HabitCompletionResponse(
                habitId,
                shouldComplete,
                completionDate.ToString("yyyy-MM-dd"),
                disciplineValue));
        }

        private static DateOnly GetCurrentLocalDate()
        {
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
            return DateOnly.FromDateTime(localNow);
        }

        private static string NormalizeName(string name)
        {
            return name.Trim().ToLowerInvariant();
        }

        private static string? NormalizeDescription(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            return description.Trim();
        }

        public sealed record CreateHabitRequest(
            string Name,
            string? Description);

        public sealed record UpdateHabitRequest(
            string Name,
            string? Description);

        public sealed record ToggleHabitCompletionRequest(
            bool IsCompleted,
            string? Date);

        public sealed record HabitResponse(
            Guid Id,
            string Name,
            string? Description,
            bool IsCompletedToday,
            int CompletedDays,
            string? LastCompletedDate,
            DateTime CreatedAtUtc,
            DateTime UpdatedAtUtc);

        public sealed record HabitsResponse(
            string Date,
            double Discipline,
            int TotalHabits,
            int CompletedHabits,
            IReadOnlyList<HabitResponse> Habits);

        public sealed record HabitCompletionResponse(
            Guid HabitId,
            bool IsCompleted,
            string Date,
            double DisciplineValue);
    }
}
