using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Validation;
using HookSentry.Api.DataTransfer.Senders.Requests;
using HookSentry.Api.DataTransfer.Senders.Responses;
using HookSentry.Domain;
using HookSentry.Domain.Destinations;
using HookSentry.Domain.Senders;

namespace HookSentry.Api.Features.Senders.CreateSender;

public class CreateSenderEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/destinations/{destinationId:guid}/senders", Handle)
            .WithName("CreateSender")
            .WithTags("Senders")
            .WithSummary("Registra um sender para uma URL de destino")
            .WithDescription("""
                Cria um WebhookSender vinculado a uma URL de destino. O sender recebe seu próprio
                ingest token — use-o como URL no serviço externo para acionar a normalização de payload.

                O `ingestToken` retornado no `201` é exibido **uma única vez** — guarde-o para configurar
                o webhook no serviço externo. Use `POST /api/v1/senders/{id}/ingest-token` para regenerar.

                **Parâmetros de rota:**
                - `destinationId` *(obrigatório)*: UUID da URL de destino

                **Body:**
                - `label` *(opcional)*: nome descritivo para identificação no dashboard (máx. 255 caracteres)

                **Códigos de retorno:**
                - `201 Created`: sender criado com ingest token
                - `400 Bad Request`: label inválido ou caracteres de controle
                - `401 Unauthorized`: token JWT ausente ou inválido
                - `403 Forbidden`: URL de destino pertence a outro tenant
                - `404 Not Found`: URL de destino não encontrada
                """)
            .RequireAuthorization()
            .Produces<CreateSenderResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid destinationId,
        CreateSenderRequest request,
        ClaimsPrincipal user,
        IDestinationUrlRepository destinationRepository,
        IWebhookSenderRepository senderRepository,
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;

        if (request.Label is not null)
        {
            if (InputSanitizer.ValidateName(request.Label) is { } labelErr)
                return Results.BadRequest(labelErr);
        }

        var destination = await destinationRepository.FindAsync(destinationId, ct);
        if (destination is null) return Results.NotFound($"Destination '{destinationId}' not found.");
        if (destination.TenantId != tenantId) return Results.Forbid();

        WebhookSender sender;
        string rawToken;
        try
        {
            sender = new WebhookSender(destinationId, tenantId, request.Label);
            rawToken = sender.RotateIngestToken();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        await using var uow = uowFactory.Create();
        await senderRepository.AddAsync(sender, ct);
        await uow.CommitAsync(ct);

        return Results.Created(
            $"/api/v1/senders/{sender.Id}",
            CreateSenderResponse.From(sender, rawToken));
    }
}
