using System.Security.Claims;
using Domain.Progression;

namespace LifeRPG.API.Endpoints
{
    public static class ProfileEndpoints
    {
        public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/profile").RequireAuthorization();

            group.MapGet("/", GetProfileAsync);

            return app;
        }

        private static async Task<IResult> GetProfileAsync(
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

            var user = await dbContext.Users
                .AsNoTracking()
                .Include(x => x.Profile)
                .ThenInclude(x => x.Attributes)
                .SingleOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

            if (user is null)
            {
                return Results.NotFound();
            }

            var experienceState = CharacterExperience.BuildState(user.Profile.TotalExperience);

            var response = new ProfileResponse(
                user.Id,
                user.Login,
                user.CreatedAtUtc,
                experienceState.Level,
                experienceState.TotalExperience,
                experienceState.ExperienceInLevel,
                experienceState.ExperienceToNextLevel,
                user.Profile.Attributes
                    .OrderBy(x => x.AttributeType)
                    .Select(x => new AttributeResponse(x.AttributeType.ToString(), x.Value))
                    .ToList());

            return Results.Ok(response);
        }

        public sealed record ProfileResponse(
            Guid UserId,
            string Login,
            DateTime CreatedAtUtc,
            int Level,
            int TotalExperience,
            int CurrentLevelExperience,
            int ExperienceToNextLevel,
            IReadOnlyList<AttributeResponse> Attributes);

        public sealed record AttributeResponse(
            string Type,
            int Value);
    }
}
