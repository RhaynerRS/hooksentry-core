using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Users.Responses;
using HookSentry.Domain.Users;

namespace HookSentry.Api.Features.Users.GetUser;

public class GetUserEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users/{id:guid}", Handle)
            .WithName("GetUserById")
            .WithTags("Users")
            .WithSummary("Returns a user's data by ID")
            .WithDescription("""
                Looks up a user by their UUID.
                The user must belong to the tenant from the JWT token (RNF-007).
                The `password` field is never returned.

                **Route parameters:**
                - `id` *(required)*: user UUID

                **Return codes:**
                - `200 OK`: user data (without password field)
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: user belongs to another tenant (RNF-007)
                - `404 Not Found`: user not found
                """)
            .RequireAuthorization()
            .Produces<UserResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal principal,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        if (principal.RequireTenantId(out var tenantId) is { } err) return err;

        var user = await userRepository.FindAsync(id, ct);

        if (user is null) return Results.NotFound();
        if (user.TenantId != tenantId) return Results.Forbid();

        return Results.Ok(UserResponse.From(user));
    }
}
