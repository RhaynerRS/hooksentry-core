using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Domain;
using HookSentry.Domain.Users;

namespace HookSentry.Api.Features.Users.DeleteUser;

public class DeleteUserEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/users/{id:guid}", Handle)
            .WithName("DeleteUser")
            .WithTags("Users")
            .WithSummary("Permanently removes a user")
            .WithDescription("""
                Permanently deletes a user from the authenticated tenant.
                Irreversible operation — use with explicit confirmation in the frontend.

                **Role restriction:** only users with `role = Admin` can perform this operation.
                The `role` claim from the JWT token is used to verify the role — never the body (RNF-007).

                **Route parameters:**
                - `id` *(required)*: UUID of the user to delete

                **Return codes:**
                - `204 No Content`: user deleted successfully
                - `401 Unauthorized`: missing, invalid token, or missing `role` claim
                - `403 Forbidden`: authenticated user is not Admin, or the target belongs to another tenant (RNF-007)
                - `404 Not Found`: user not found
                """)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal principal,
        IUserRepository userRepository,
        IUnitOfWorkFactory uowFactory,
        ILogger<DeleteUserEndpoint> logger,
        CancellationToken ct)
    {
        if (principal.RequireAdminRole(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var user = await userRepository.FindAsync(id, ct);

        if (user is null) return Results.NotFound();
        if (user.TenantId != tenantId) return Results.Forbid();

        await userRepository.RemoveAsync(user, ct);
        await uow.CommitAsync(ct);

        logger.LogInformation(
            "User deleted. TenantId={TenantId} UserId={UserId} ActorEmail={ActorEmail}",
            tenantId, id, principal.GetEmail());

        return Results.NoContent();
    }
}
