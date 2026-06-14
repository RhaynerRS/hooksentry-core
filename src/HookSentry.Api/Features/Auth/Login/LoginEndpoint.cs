using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Services;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain.Security;
using HookSentry.Domain.Users;
using HookSentry.Infrastructure.Auth;
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

                **Body:**
                - `email` *(required)*: user email
                - `password` *(required)*: user password

                **Return codes:**
                - `200 OK`: authentication successful
                - `400 Bad Request`: missing required fields
                - `401 Unauthorized`: invalid credentials or inactive user
                """)
            .AllowAnonymous()
            .Produces<AuthResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        LoginRequest request,
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IPasswordHasher passwordHasher,
        IRefreshTokenStore tokenStore,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest("Password is required.");

        if (InputSanitizer.ValidateEmail(request.Email) is { } emailErr)
            return Results.BadRequest(emailErr);

        var user = await userRepository.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        if (user.Status != UserStatus.Active)
            return Results.Unauthorized();

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
