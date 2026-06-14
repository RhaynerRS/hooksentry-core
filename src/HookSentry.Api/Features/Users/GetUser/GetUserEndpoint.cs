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
            .WithSummary("Retorna os dados de um usuário pelo ID")
            .WithDescription("""
                Busca um usuário pelo seu UUID.
                O usuário deve pertencer ao tenant do token JWT (RNF-007).
                O campo `password` nunca é retornado.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do usuário

                **Códigos de retorno:**
                - `200 OK`: dados do usuário (sem campo password)
                - `401 Unauthorized`: token ausente ou inválido
                - `403 Forbidden`: usuário pertence a outro tenant (RNF-007)
                - `404 Not Found`: usuário não encontrado
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
