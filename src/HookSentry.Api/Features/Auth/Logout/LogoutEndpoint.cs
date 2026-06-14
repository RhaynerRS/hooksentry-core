using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Validation;
using HookSentry.Infrastructure.Auth;
using HookSentry.Api.DataTransfer.Auth.Requests;

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
                Removes the refresh token from Redis, preventing it from being reused.
                The access token (JWT) remains valid until it expires naturally (15 minutes).

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
        return Results.NoContent();
    }
}
