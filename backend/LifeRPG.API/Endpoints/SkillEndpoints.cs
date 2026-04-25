using System.Security.Claims;

namespace LifeRPG.API.Endpoints
{
    public static class SkillEndpoints
    {
        public static IEndpointRouteBuilder MapSkillEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/skills").RequireAuthorization();

            group.MapGet("/", GetSkillsAsync);
            group.MapPost("/", CreateSkillAsync);
            group.MapPatch("/{userSkillId:guid}", UpdateSkillAsync);
            group.MapDelete("/{userSkillId:guid}", DeleteSkillAsync);

            return app;
        }

        private static async Task<IResult> GetSkillsAsync(
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

            var skills = await dbContext.UserSkills
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .Include(x => x.Attributes)
                .OrderBy(x => x.Name)
                .Select(x => new SkillResponse(
                    x.Id,
                    x.Name,
                    x.Level,
                    x.CurrentUses,
                    x.RequiredUsesForNextLevel,
                    x.Attributes
                        .Where(a => a.AttributeType != AttributeType.Discipline)
                        .OrderBy(a => a.AttributeType)
                        .Select(a => a.AttributeType.ToString())
                        .ToList()))
                .ToListAsync(cancellationToken);

            return Results.Ok(skills);
        }

        private static async Task<IResult> CreateSkillAsync(
            CreateUserSkillRequest request,
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

            if (string.IsNullOrWhiteSpace(name) || name.Length is < 2 or > 100)
            {
                return Results.BadRequest(new { error = "Название навыка должно быть от 2 до 100 символов." });
            }

            var normalizedName = NormalizeName(name);

            var duplicateExists = await dbContext.UserSkills
                .Where(x => x.UserId == userId.Value)
                .AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken);

            if (duplicateExists)
            {
                return Results.Conflict(new { error = "Навык с таким названием уже существует." });
            }

            var now = DateTime.UtcNow;
            var attributes = NormalizeAttributes(request.Attributes);

            if (attributes.Contains(AttributeType.Discipline))
            {
                return Results.BadRequest(new { error = "Дисциплину больше нельзя связывать с навыком." });
            }

            var userSkill = new UserSkill
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Name = name,
                NormalizedName = normalizedName,
                Level = 1,
                CurrentUses = 0,
                RequiredUsesForNextLevel = 7,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Attributes = attributes
                    .Select(attributeType => new UserSkillAttribute
                    {
                        UserSkillId = Guid.Empty,
                        AttributeType = attributeType
                    })
                    .ToList()
            };

            foreach (var attribute in userSkill.Attributes)
            {
                attribute.UserSkillId = userSkill.Id;
            }

            dbContext.UserSkills.Add(userSkill);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new SkillResponse(
                userSkill.Id,
                userSkill.Name,
                userSkill.Level,
                userSkill.CurrentUses,
                userSkill.RequiredUsesForNextLevel,
                userSkill.Attributes
                    .Where(a => a.AttributeType != AttributeType.Discipline)
                    .OrderBy(a => a.AttributeType)
                    .Select(a => a.AttributeType.ToString())
                    .ToList());

            return Results.Ok(response);
        }

        private static async Task<IResult> UpdateSkillAsync(
            Guid userSkillId,
            UpdateUserSkillRequest request,
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

            var newName = request.Name.Trim();

            if (string.IsNullOrWhiteSpace(newName) || newName.Length is < 2 or > 100)
            {
                return Results.BadRequest(new { error = "Название навыка должно быть от 2 до 100 символов." });
            }

            var userSkill = await dbContext.UserSkills
                .Include(x => x.Attributes)
                .SingleOrDefaultAsync(
                    x => x.Id == userSkillId &&
                         x.UserId == userId.Value,
                    cancellationToken);

            if (userSkill is null)
            {
                return Results.NotFound();
            }

            var normalizedName = NormalizeName(newName);

            var duplicateExists = await dbContext.UserSkills
                .Where(x => x.UserId == userId.Value && x.Id != userSkillId)
                .AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken);

            if (duplicateExists)
            {
                return Results.Conflict(new { error = "Навык с таким названием уже существует." });
            }

            var attributes = NormalizeAttributes(request.Attributes);

            if (attributes.Contains(AttributeType.Discipline))
            {
                return Results.BadRequest(new { error = "Дисциплину больше нельзя связывать с навыком." });
            }

            userSkill.Name = newName;
            userSkill.NormalizedName = normalizedName;
            userSkill.UpdatedAtUtc = DateTime.UtcNow;

            userSkill.Attributes.Clear();
            foreach (var attributeType in attributes)
            {
                userSkill.Attributes.Add(new UserSkillAttribute
                {
                    UserSkillId = userSkill.Id,
                    AttributeType = attributeType
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new SkillResponse(
                userSkill.Id,
                userSkill.Name,
                userSkill.Level,
                userSkill.CurrentUses,
                userSkill.RequiredUsesForNextLevel,
                userSkill.Attributes
                    .Where(a => a.AttributeType != AttributeType.Discipline)
                    .OrderBy(a => a.AttributeType)
                    .Select(a => a.AttributeType.ToString())
                    .ToList());

            return Results.Ok(response);
        }

        private static async Task<IResult> DeleteSkillAsync(
            Guid userSkillId,
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

            var userSkill = await dbContext.UserSkills
                .SingleOrDefaultAsync(
                    x => x.Id == userSkillId &&
                         x.UserId == userId.Value,
                    cancellationToken);

            if (userSkill is null)
            {
                return Results.NotFound();
            }

            dbContext.UserSkills.Remove(userSkill);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }

        private static string NormalizeName(string name)
        {
            return name.Trim().ToLowerInvariant();
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

        public sealed record CreateUserSkillRequest(
            string Name,
            IReadOnlyList<AttributeType>? Attributes);

        public sealed record UpdateUserSkillRequest(
            string Name,
            IReadOnlyList<AttributeType>? Attributes);

        public sealed record SkillResponse(
            Guid UserSkillId,
            string Name,
            int Level,
            int CurrentUses,
            int RequiredUsesForNextLevel,
            IReadOnlyList<string> Attributes);
    }
}
