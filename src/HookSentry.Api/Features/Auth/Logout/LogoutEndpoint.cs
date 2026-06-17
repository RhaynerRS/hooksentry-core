using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Validation;
using HookSentry.Infrastructure.Auth;
using HookSentry.Api.DataTransfer.Auth.Requests;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HookSentry.Api.Features.Auth.Logout;

public class LogoutEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/logout", Handle)
            .WithName("Logout")
            .WithTags("Auth")
            .WithSummary("Invalidates the authenticated user's refresh token")
            .WithDescription("""
                Removes the refresh token from Redis and immediately revokes the access token (JWT)
                via a Redis denylist keyed by the token's `jti`. Any subsequent request with the
                same access token receives `401 Unauthorized`.

                **Body:**
                - `refreshToken` *(required)*: refresh token to invalidate

                **Return codes:**
                - `204 No Content`: logout successful
                - `400 Bad Request`: refreshToken missing
                - `401 Unauthorized`: missing or invalid JWT, or refresh token does not belong to the user
                """)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> Handle(
        LogoutRequest request,
        ClaimsPrincipal user,
        IRefreshTokenStore tokenStore,
        IJwtDenylist denylist,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Results.BadRequest("RefreshToken is required.");
        if (InputSanitizer.ValidateToken(request.RefreshToken) is { } tokenErr)
            return Results.BadRequest(tokenErr);

        if (!Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? user.FindFirst("sub")?.Value, out var userId))
            return Results.Unauthorized();

        var stored = await tokenStore.GetAsync(request.RefreshToken);
        if (stored is null)
            return Results.Unauthorized();

        if (stored.Value.UserId != userId)
            return Results.Unauthorized();

        await tokenStore.RemoveAsync(request.RefreshToken);

        var jti = user.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (jti is not null)
        {
            var expClaim = user.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            var remainingTtl = expClaim is not null
                && long.TryParse(expClaim, out var expUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(expUnix) - DateTimeOffset.UtcNow
                : TimeSpan.FromMinutes(15);

            if (remainingTtl > TimeSpan.Zero)
                await denylist.RevokeAsync(jti, remainingTtl, ct);
        }

        return Results.NoContent();
    }
}
