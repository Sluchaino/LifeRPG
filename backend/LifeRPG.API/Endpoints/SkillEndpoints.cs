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
                        .ToList(),
                    x.Attributes
                        .Where(a => a.AttributeType != AttributeType.Discipline)
                        .OrderBy(a => a.AttributeType)
                        .Select(a => new SkillAttributeShareResponse(
                            a.AttributeType.ToString(),
                            a.SharePercent))
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

            var normalizationResult = NormalizeAttributeShares(request.AttributeShares, request.Attributes);
            if (!normalizationResult.IsValid)
            {
                return Results.BadRequest(new { error = normalizationResult.ErrorMessage });
            }

            var attributeShares = normalizationResult.Value!;

            if (attributeShares.Any(x => x.AttributeType == AttributeType.Discipline))
            {
                return Results.BadRequest(new { error = "Дисциплину больше нельзя связывать с навыком." });
            }

            var now = DateTime.UtcNow;
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
                Attributes = attributeShares
                    .Select(attributeShare => new UserSkillAttribute
                    {
                        UserSkillId = Guid.Empty,
                        AttributeType = attributeShare.AttributeType,
                        SharePercent = attributeShare.SharePercent
                    })
                    .ToList()
            };

            foreach (var attribute in userSkill.Attributes)
            {
                attribute.UserSkillId = userSkill.Id;
            }

            dbContext.UserSkills.Add(userSkill);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToSkillResponse(userSkill));
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

            var normalizationResult = NormalizeAttributeShares(request.AttributeShares, request.Attributes);
            if (!normalizationResult.IsValid)
            {
                return Results.BadRequest(new { error = normalizationResult.ErrorMessage });
            }

            var attributeShares = normalizationResult.Value!;

            if (attributeShares.Any(x => x.AttributeType == AttributeType.Discipline))
            {
                return Results.BadRequest(new { error = "Дисциплину больше нельзя связывать с навыком." });
            }

            userSkill.Name = newName;
            userSkill.NormalizedName = normalizedName;
            userSkill.UpdatedAtUtc = DateTime.UtcNow;

            userSkill.Attributes.Clear();
            foreach (var attributeShare in attributeShares)
            {
                userSkill.Attributes.Add(new UserSkillAttribute
                {
                    UserSkillId = userSkill.Id,
                    AttributeType = attributeShare.AttributeType,
                    SharePercent = attributeShare.SharePercent
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToSkillResponse(userSkill));
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

        private static SkillResponse ToSkillResponse(UserSkill userSkill)
        {
            return new SkillResponse(
                userSkill.Id,
                userSkill.Name,
                userSkill.Level,
                userSkill.CurrentUses,
                userSkill.RequiredUsesForNextLevel,
                userSkill.Attributes
                    .Where(a => a.AttributeType != AttributeType.Discipline)
                    .OrderBy(a => a.AttributeType)
                    .Select(a => a.AttributeType.ToString())
                    .ToList(),
                userSkill.Attributes
                    .Where(a => a.AttributeType != AttributeType.Discipline)
                    .OrderBy(a => a.AttributeType)
                    .Select(a => new SkillAttributeShareResponse(
                        a.AttributeType.ToString(),
                        a.SharePercent))
                    .ToList());
        }

        private static AttributeSharesNormalizationResult NormalizeAttributeShares(
            IReadOnlyList<SkillAttributeShareRequest>? attributeShares,
            IReadOnlyList<AttributeType>? attributes)
        {
            if (attributeShares is not null && attributeShares.Count > 0)
            {
                var normalizedShares = attributeShares
                    .Select(x => new NormalizedAttributeShare(x.AttributeType, x.Percent))
                    .OrderBy(x => x.AttributeType)
                    .ToList();

                if (normalizedShares.Any(x => x.SharePercent <= 0 || x.SharePercent > 100))
                {
                    return AttributeSharesNormalizationResult.Invalid("Процент распределения должен быть от 1 до 100.");
                }

                var hasDuplicates = normalizedShares
                    .GroupBy(x => x.AttributeType)
                    .Any(x => x.Count() > 1);

                if (hasDuplicates)
                {
                    return AttributeSharesNormalizationResult.Invalid("Характеристики в распределении не должны повторяться.");
                }

                var totalPercent = normalizedShares.Sum(x => x.SharePercent);
                if (totalPercent != 100)
                {
                    return AttributeSharesNormalizationResult.Invalid("Сумма процентов распределения должна быть равна 100.");
                }

                return AttributeSharesNormalizationResult.Valid(normalizedShares);
            }

            if (attributes is null || attributes.Count == 0)
            {
                return AttributeSharesNormalizationResult.Valid(Array.Empty<NormalizedAttributeShare>());
            }

            var normalizedAttributes = attributes
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var baseShare = 100 / normalizedAttributes.Count;
            var remainder = 100 - baseShare * normalizedAttributes.Count;
            var result = new List<NormalizedAttributeShare>(normalizedAttributes.Count);

            for (var index = 0; index < normalizedAttributes.Count; index++)
            {
                var share = baseShare + (index < remainder ? 1 : 0);
                result.Add(new NormalizedAttributeShare(normalizedAttributes[index], share));
            }

            return AttributeSharesNormalizationResult.Valid(result);
        }

        public sealed record CreateUserSkillRequest(
            string Name,
            IReadOnlyList<AttributeType>? Attributes,
            IReadOnlyList<SkillAttributeShareRequest>? AttributeShares);

        public sealed record UpdateUserSkillRequest(
            string Name,
            IReadOnlyList<AttributeType>? Attributes,
            IReadOnlyList<SkillAttributeShareRequest>? AttributeShares);

        public sealed record SkillAttributeShareRequest(
            AttributeType AttributeType,
            int Percent);

        public sealed record SkillResponse(
            Guid UserSkillId,
            string Name,
            int Level,
            int CurrentUses,
            int RequiredUsesForNextLevel,
            IReadOnlyList<string> Attributes,
            IReadOnlyList<SkillAttributeShareResponse> AttributeShares);

        public sealed record SkillAttributeShareResponse(
            string AttributeType,
            int Percent);

        private sealed record NormalizedAttributeShare(
            AttributeType AttributeType,
            int SharePercent);

        private sealed record AttributeSharesNormalizationResult(
            bool IsValid,
            string? ErrorMessage,
            IReadOnlyList<NormalizedAttributeShare>? Value)
        {
            public static AttributeSharesNormalizationResult Valid(
                IReadOnlyList<NormalizedAttributeShare> value) =>
                new(true, null, value);

            public static AttributeSharesNormalizationResult Invalid(string errorMessage) =>
                new(false, errorMessage, null);
        }
    }
}
