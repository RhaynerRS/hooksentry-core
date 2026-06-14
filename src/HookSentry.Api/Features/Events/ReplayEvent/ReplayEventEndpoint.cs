using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Events.Responses;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Events;
using HookSentry.Domain.Tenants;
using HookSentry.Infrastructure.RabbitMq;

namespace HookSentry.Api.Features.Events.ReplayEvent;

public class ReplayEventEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/events/{id:guid}/replay", Handle)
            .WithName("ReplayEvent")
            .WithTags("Events")
            .WithSummary("Reenvia manualmente um evento com status CriticalFailure")
            .WithDescription("""
                Reprocessa um evento que esgotou todas as tentativas automáticas (RF-013).

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do evento a ser reenviado

                **Pré-condição:** o evento deve estar com status `CriticalFailure`.
                Qualquer outro status retorna `400 Bad Request`.

                **Efeitos ao reprocessar:**
                - `currentRetryCount` é zerado
                - `nextAttemptAt` é definido como `now()`
                - `status` é alterado para `Pending`

                Retorna `403 Forbidden` se o evento pertencer a outro tenant (RNF-007).
                """)
            .RequireAuthorization()
            .Produces<EventResponse>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal user,
        NHibernate.ISession session,
        IEventPublisher publisher,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        using var tx = session.BeginTransaction();

        var evento = await session.GetAsync<Event>(id, ct);

        if (evento is null)
            return Results.NotFound();

        if (evento.TenantId != tenantId)
            return Results.Forbid();

        var destination = await session.GetAsync<DestinationUrl>(evento.DestinationUrlId, ct);
        var tenant = await session.GetAsync<Tenant>(evento.TenantId, ct);

        if (destination is null || tenant is null)
            return Results.Problem("Dados do destino ou tenant não encontrados.");

        try { evento.ResetForReplay(); }
        catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }

        await tx.CommitAsync(ct);

        await publisher.PublishAsync(new EventMessage(
            EventId: evento.Id,
            TenantId: evento.TenantId,
            DestinationUrlId: evento.DestinationUrlId,
            DestinationUrl: destination.Url,
            Payload: evento.Payload,
            RetryCount: 0,
            MaxTrys: tenant.MaxTrys,
            WebhookSecret: tenant.WebhookSecret,
            ServerRateLimit: destination.ServerRateLimit,
            AuthType: destination.AuthType,
            CredentialsEncrypted: destination.CredentialsEncrypted
        ), ct);

        return Results.Ok(EventResponse.From(evento));
    }
}
