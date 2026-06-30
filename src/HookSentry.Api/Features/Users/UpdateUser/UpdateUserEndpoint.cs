using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain;
using HookSentry.Domain.Security;
using HookSentry.Api.DataTransfer.Users.Requests;
using HookSentry.Api.DataTransfer.Users.Responses;
using HookSentry.Domain.Users;

namespace HookSentry.Api.Features.Users.UpdateUser;

public class UpdateUserEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/v1/users/{id:guid}", Handle)
            .WithName("UpdateUser")
            .WithTags("Users")
            .WithSummary("Partially updates a user")
            .WithDescription("""
                Updates fields of a user belonging to the authenticated tenant (RNF-007).
                Only fields included in the body are changed — others remain unchanged.

                **Route parameters:**
                - `id` *(required)*: user UUID

                **Body** *(all fields optional):*
                - `email`: new unique email address on the platform (RN-001)
                - `password`: new plain text password — will be stored as hash
                - `role`: new role — `0` = Developer, `1` = Admin
                - `status`: new status — `0` = Active, `1` = Inactive

                **Return codes:**
                - `200 OK`: user updated successfully
                - `400 Bad Request`: invalid value (malformed email, role out of domain)
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: user belongs to another tenant (RNF-007)
                - `404 Not Found`: user not found
                - `409 Conflict`: email is already in use by another user (RN-001)
                """)
            .RequireAuthorization()
            .Produces<UserResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> Handle(
        Guid id,
        UpdateUserRequest request,
        ClaimsPrincipal principal,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        IUnitOfWorkFactory uowFactory,
        ILogger<UpdateUserEndpoint> logger,
        CancellationToken ct)
    {
        if (principal.RequireTenantId(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var user = await userRepository.FindAsync(id, ct);

        if (user is null) return Results.NotFound();
        if (user.TenantId != tenantId) return Results.Forbid();

        if (request.Email is not null)
        {
            if (InputSanitizer.ValidateEmail(request.Email) is { } emailErr)
                return Results.BadRequest(emailErr);

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (await userRepository.EmailExistsExcludingAsync(normalizedEmail, id, ct))
                return Results.Conflict($"Email '{request.Email}' is already in use.");
        }

        if (request.Role is not null)
        {
            var callerRole = principal.GetUserRole();
            if (callerRole != UserRole.Admin && callerRole != UserRole.Owner)
                return Results.Forbid();
            if (request.Role == UserRole.Owner && callerRole != UserRole.Owner)
                return Results.Forbid();
        }

        try
        {
            if (request.Email is not null) user.SetEmail(request.Email);
            if (request.Password is not null) user.SetPasswordHash(passwordHasher.Hash(request.Password));
            if (request.Role is not null) user.SetRole(request.Role.Value);
            if (request.Status == UserStatus.Active) user.Activate();
            else if (request.Status == UserStatus.Inactive) user.Deactivate();
        }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        await uow.CommitAsync(ct);

        logger.LogInformation(
            "User updated. TenantId={TenantId} UserId={UserId} ActorEmail={ActorEmail}",
            tenantId, id, principal.GetEmail());

        return Results.Ok(UserResponse.From(user));
    }
}
