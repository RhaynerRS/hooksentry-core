using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Services;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain.Security;
using HookSentry.Domain.Users;
using HookSentry.Infrastructure.Auth;
using HookSentry.Infrastructure.RateLimiting;
using HookSentry.Api.DataTransfer.Auth.Requests;
using HookSentry.Api.DataTransfer.Auth.Responses;

namespace HookSentry.Api.Features.Auth.Login;

public class LoginEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/login", Handle)
            .WithName("Login")
            .WithTags("Auth")
            .WithSummary("Authenticates a user and returns access tokens")
            .WithDescription("""
                Authenticates an existing user and returns a JWT access token (TTL: 15 minutes) and a refresh token (TTL: 7 days).
                The refresh token is stored in Redis. The JWT is stateless — validated only by signature and expiration.

                **Rate limiting:** blocked after 10 failed attempts per IP or per email within a 5-minute window.
                The counter resets automatically after the window expires, or immediately on a successful login.

                **Body:**
                - `email` *(required)*: user email
                - `password` *(required)*: user password

                **Return codes:**
                - `200 OK`: authentication successful
                - `400 Bad Request`: missing required fields or invalid email format
                - `401 Unauthorized`: invalid credentials or inactive user
                - `429 Too Many Requests`: rate limit exceeded — too many failed attempts from this IP or for this email
                """)
            .AllowAnonymous()
            .Produces<AuthResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<string>(StatusCodes.Status429TooManyRequests);
    }

    private const string LoginAction = "login";

    // Dummy hash used to equalize response time when the email is not found,
    // preventing timing-based user enumeration. Format: base64(16-byte salt):base64(32-byte hash).
    private const string DummyHash = "AAAAAAAAAAAAAAAAAAAAAA==:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private static async Task<IResult> Handle(
        LoginRequest request,
        HttpContext httpContext,
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IPasswordHasher passwordHasher,
        IRefreshTokenStore tokenStore,
        IPublicEndpointRateLimiter rateLimiter,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest("Password is required.");

        if (InputSanitizer.ValidateEmail(request.Email) is { } emailErr)
            return Results.BadRequest(emailErr);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (await rateLimiter.IsBlockedAsync(LoginAction, ip, ct) ||
            await rateLimiter.IsBlockedAsync(LoginAction, normalizedEmail, ct))
            return Results.Json(
                "Too many failed login attempts. Try again in 5 minutes.",
                statusCode: StatusCodes.Status429TooManyRequests);

        var user = await userRepository.FindByEmailAsync(normalizedEmail, ct);

        // Always run Verify — even when user is null — to prevent timing-based user enumeration.
        var hashToVerify = user?.PasswordHash ?? DummyHash;
        var credentialsValid = passwordHasher.Verify(request.Password, hashToVerify);

        if (user is null || !credentialsValid || user.Status != UserStatus.Active)
        {
            if (user is not null)
            {
                await rateLimiter.RecordAsync(LoginAction, ip, ct);
                await rateLimiter.RecordAsync(LoginAction, normalizedEmail, ct);
            }
            return Results.Unauthorized();
        }

        await rateLimiter.ResetAsync(LoginAction, ip, ct);
        await rateLimiter.ResetAsync(LoginAction, normalizedEmail, ct);

        var (accessToken, _, expiresAt) = jwtTokenService.GenerateAccessToken(user);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        await tokenStore.StoreAsync(refreshToken, user.Id, user.TenantId);

        return Results.Ok(new AuthResponse(
            accessToken,
            (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds,
            refreshToken,
            expiresAt));
    }
}
