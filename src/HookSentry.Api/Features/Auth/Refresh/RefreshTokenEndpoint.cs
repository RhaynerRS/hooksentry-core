using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Services;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain.Users;
using HookSentry.Infrastructure.Auth;
using HookSentry.Api.DataTransfer.Auth.Requests;
using HookSentry.Api.DataTransfer.Auth.Responses;

namespace HookSentry.Api.Features.Auth.Refresh;

public class RefreshTokenEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/refresh", Handle)
            .WithName("RefreshToken")
            .WithTags("Auth")
            .WithSummary("Renews the access token using a valid refresh token")
            .WithDescription("""
                Exchanges an active refresh token for a new token pair (access token + refresh token).
                The provided refresh token is consumed atomically (single-use via Redis GETDEL).

                **Body:**
                - `refreshToken` *(required)*: refresh token obtained at login or from a previous refresh

                **Return codes:**
                - `200 OK`: new tokens issued successfully
                - `400 Bad Request`: required field missing
                - `401 Unauthorized`: invalid, expired, or already-used refresh token
                """)
            .AllowAnonymous()
            .Produces<AuthResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        RefreshTokenRequest request,
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IRefreshTokenStore tokenStore,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Results.BadRequest("RefreshToken is required.");
        if (InputSanitizer.ValidateToken(request.RefreshToken) is { } tokenErr)
            return Results.BadRequest(tokenErr);

        var stored = await tokenStore.ConsumeAsync(request.RefreshToken);
        if (stored is null)
            return Results.Unauthorized();

        var (userId, tenantId) = stored.Value;
        var user = await userRepository.FindAsync(userId, ct);
        if (user is null || user.Status != UserStatus.Active || user.TenantId != tenantId)
            return Results.Unauthorized();

        var (accessToken, _, expiresAt) = jwtTokenService.GenerateAccessToken(user);
        var newRefreshToken = jwtTokenService.GenerateRefreshToken();

        await tokenStore.StoreAsync(newRefreshToken, user.Id, user.TenantId);

        return Results.Ok(new AuthResponse(
            accessToken,
            (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds,
            newRefreshToken,
            expiresAt));
    }
}
