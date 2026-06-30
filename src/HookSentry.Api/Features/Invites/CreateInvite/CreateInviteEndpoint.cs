using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Invites.Requests;
using HookSentry.Api.DataTransfer.Invites.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Invites;
using HookSentry.Domain.Tenants;
using HookSentry.Domain.Users;

namespace HookSentry.Api.Features.Invites.CreateInvite;

public class CreateInviteEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/invites", Handle)
            .WithName("CreateInvite")
            .WithTags("Invites")
            .WithSummary("Generates a pre-signed registration link for the tenant")
            .WithDescription("""
                Creates an invite token that allows a new user to register in the tenant
                without prior credentials. Admin and Owner can create invites.

                **Requires authentication with Admin or Owner role.**

                **Body:**
                - `validityDays` *(optional, default: 7)*: invite validity in days — minimum 1, maximum 30
                - `role` *(optional, default: Developer)*: role assigned on registration — `Developer`, `Viewer`, or `Admin` (Owner only)

                **Return codes:**
                - `201 Created`: invite created — the `token` field composes the registration link
                - `400 Bad Request`: validityDays outside the allowed range (1–30), or invalid role
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: caller lacks required role, or Admin attempts to invite another Admin
                - `404 Not Found`: tenant not found
                """)
            .RequireAuthorization()
            .Produces<InviteTokenResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        CreateInviteRequest request,
        ClaimsPrincipal principal,
        ITenantRepository tenantRepository,
        IInviteTokenRepository inviteRepository,
        IUnitOfWorkFactory uowFactory,
        ILogger<CreateInviteEndpoint> logger,
        CancellationToken ct)
    {
        if (principal.RequireAdminRole(out var tenantId) is { } err) return err;

        var targetRole = request.Role ?? UserRole.Developer;

        if (targetRole == UserRole.Owner)
            return Results.BadRequest("Owner role cannot be assigned via invite.");

        if (targetRole == UserRole.Admin && principal.GetUserRole() != UserRole.Owner)
            return Results.Forbid();

        var tenant = await tenantRepository.FindAsync(tenantId, ct);
        if (tenant is null)
            return Results.NotFound($"Tenant '{tenantId}' not found.");

        InviteToken invite;
        try { invite = new InviteToken(tenantId, request.ValidityDays, targetRole); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        await using var uow = uowFactory.Create();
        await inviteRepository.AddAsync(invite, ct);
        await uow.CommitAsync(ct);

        logger.LogInformation(
            "Invite provisioned. TenantId={TenantId} InviteId={InviteId} TargetRole={TargetRole} ExpiresAt={ExpiresAt} ActorEmail={ActorEmail}",
            tenantId, invite.Id, invite.TargetRole, invite.ExpiresAt, principal.GetEmail());

        return Results.Created($"/api/v1/invites/{invite.Id}", InviteTokenResponse.From(invite));
    }
}
