using System.Security.Claims;

namespace LifeRPG.API.Endpoints
{
    public static class AuthEndpoints
    {
        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/auth");

            group.MapPost("/register", RegisterAsync);
            group.MapPost("/login", LoginAsync);
            group.MapPost("/logout", LogoutAsync).RequireAuthorization();
            group.MapGet("/me", MeAsync).RequireAuthorization();

            return app;
        }

        private static async Task<IResult> RegisterAsync(
            RegisterRequest request,
            HttpContext httpContext,
            AppDbContext dbContext,
            IPasswordService passwordService,
            ICookieAuthService cookieAuthService,
            CancellationToken cancellationToken)
        {
            var login = request.Login.Trim();
            var password = request.Password.Trim();

            if (string.IsNullOrWhiteSpace(login) || login.Length is < 3 or > 50)
            {
                return Results.BadRequest(new { error = "Логин должен быть от 3 до 50 символов." });
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 100)
            {
                return Results.BadRequest(new { error = "Пароль должен быть от 8 до 100 символов." });
            }

            var loginExists = await dbContext.Users
                .AnyAsync(x => x.Login.ToLower() == login.ToLower(), cancellationToken);

            if (loginExists)
            {
                return Results.Conflict(new { error = "Пользователь с таким логином уже существует." });
            }

            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var user = new User
            {
                Id = userId,
                Login = login,
                CreatedAtUtc = now
            };

            user.PasswordHash = passwordService.HashPassword(user, password);

            var profile = new CharacterProfile
            {
                Id = profileId,
                UserId = userId,
                Level = 1,
                TotalExperience = 0,
                CreatedAtUtc = now,
                Attributes = Enum.GetValues<AttributeType>()
                    .Select(attributeType => new CharacterAttribute
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profileId,
                        AttributeType = attributeType,
                        Value = 0,
                        UpdatedAtUtc = now
                    })
                    .ToList()
            };

            user.Profile = profile;

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            await cookieAuthService.SignInAsync(httpContext, user);

            return Results.Ok(new AuthResponse(user.Id, user.Login));
        }

        private static async Task<IResult> LoginAsync(
            LoginRequest request,
            HttpContext httpContext,
            AppDbContext dbContext,
            IPasswordService passwordService,
            ICookieAuthService cookieAuthService,
            CancellationToken cancellationToken)
        {
            var login = request.Login.Trim();
            var password = request.Password.Trim();

            var user = await dbContext.Users
                .SingleOrDefaultAsync(x => x.Login.ToLower() == login.ToLower(), cancellationToken);

            if (user is null)
            {
                return Results.Unauthorized();
            }

            var validPassword = passwordService.VerifyPassword(user, user.PasswordHash, password);

            if (!validPassword)
            {
                return Results.Unauthorized();
            }

            await cookieAuthService.SignInAsync(httpContext, user);

            return Results.Ok(new AuthResponse(user.Id, user.Login));
        }

        private static async Task<IResult> LogoutAsync(
            HttpContext httpContext,
            ICookieAuthService cookieAuthService)
        {
            await cookieAuthService.SignOutAsync(httpContext);
            return Results.NoContent();
        }

        private static async Task<IResult> MeAsync(
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
                .SingleOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

            if (user is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new AuthResponse(user.Id, user.Login));
        }

        public sealed record RegisterRequest(string Login, string Password);
        public sealed record LoginRequest(string Login, string Password);
        public sealed record AuthResponse(Guid Id, string Login);
    }
}
