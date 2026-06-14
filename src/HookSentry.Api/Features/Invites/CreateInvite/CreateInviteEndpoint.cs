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
            .WithSummary("Gera um link pré-assinado para cadastro no tenant")
            .WithDescription("""
                Cria um token de convite que permite um novo usuário se cadastrar no tenant
                sem precisar de credenciais prévias. Apenas administradores podem gerar convites.

                **Requer autenticação com role Admin.**

                **Body:**
                - `validityDays` *(opcional, padrão: 7)*: validade do convite em dias — mínimo 1, máximo 30

                **Códigos de retorno:**
                - `201 Created`: convite criado — o campo `token` compõe o link de cadastro
                - `400 Bad Request`: validityDays fora do intervalo permitido (1–30)
                - `401 Unauthorized`: token ausente ou inválido
                - `403 Forbidden`: usuário não tem role Admin
                - `404 Not Found`: tenant não encontrado
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

        return Results.Created($"/api/v1/invites/{invite.Id}", InviteTokenResponse.From(invite));
    }
}
