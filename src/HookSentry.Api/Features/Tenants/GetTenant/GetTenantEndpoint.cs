using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.DataTransfer.Tenants.Responses;
using HookSentry.Domain.Tenants;

namespace HookSentry.Api.Features.Tenants.GetTenant;

public class GetTenantEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenants/{id:guid}", Handle)
            .WithName("GetTenantById")
            .WithTags("Tenants")
            .WithSummary("Retorna os dados de um tenant pelo ID")
            .WithDescription("""
                Busca um tenant pelo seu UUID.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do tenant

                **Códigos de retorno:**
                - `200 OK`: dados do tenant (sem `webhookSecret`)
                - `401 Unauthorized`: token ausente ou inválido
                - `404 Not Found`: tenant não encontrado
                """)
            .RequireAuthorization()
            .Produces<TenantResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        ITenantRepository tenantRepository,
        CancellationToken ct)
    {
        var tenant = await tenantRepository.FindAsync(id, ct);

        return tenant is null
            ? Results.NotFound()
            : Results.Ok(new TenantResponse(
                tenant.Id,
                tenant.Name,
                tenant.MaxTrys,
                tenant.CircuitBreakerTimer,
                tenant.CreatedAt,
                tenant.UpdatedAt));
    }
}
