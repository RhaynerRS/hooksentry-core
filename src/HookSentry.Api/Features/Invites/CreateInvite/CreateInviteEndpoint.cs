using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Invites.Requests;
using HookSentry.Api.DataTransfer.Invites.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Invites;
using HookSentry.Domain.Tenants;

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
                without prior credentials. Only administrators can generate invites.

                **Requires authentication with Admin role.**

                **Body:**
                - `validityDays` *(optional, default: 7)*: invite validity in days — minimum 1, maximum 30

                **Return codes:**
                - `201 Created`: invite created — the `token` field composes the registration link
                - `400 Bad Request`: validityDays outside the allowed range (1–30)
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: user does not have Admin role
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

        var tenant = await tenantRepository.FindAsync(tenantId, ct);
        if (tenant is null)
            return Results.NotFound($"Tenant '{tenantId}' not found.");

        InviteToken invite;
        try { invite = new InviteToken(tenantId, request.ValidityDays); }
        catch (ArgumentOutOfRangeException ex) { return Results.BadRequest(ex.Message); }

        await using var uow = uowFactory.Create();
        await inviteRepository.AddAsync(invite, ct);
        await uow.CommitAsync(ct);

        logger.LogInformation(
            "Invite provisioned. TenantId={TenantId} InviteId={InviteId} ExpiresAt={ExpiresAt} ActorEmail={ActorEmail}",
            tenantId, invite.Id, invite.ExpiresAt, principal.GetEmail());

        return Results.Created($"/api/v1/invites/{invite.Id}", InviteTokenResponse.From(invite));
    }
}
