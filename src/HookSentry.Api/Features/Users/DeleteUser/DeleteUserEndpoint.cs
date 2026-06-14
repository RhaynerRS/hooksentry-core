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
            .WithSummary("Remove permanentemente um usuário")
            .WithDescription("""
                Exclui definitivamente um usuário do tenant autenticado.
                Operação irreversível — use com confirmação explícita no frontend.

                **Restrição de perfil:** apenas usuários com `role = Admin` podem executar esta operação.
                O claim `role` do token JWT é usado para verificar o perfil — nunca o body (RNF-007).

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do usuário a ser excluído

                **Códigos de retorno:**
                - `204 No Content`: usuário excluído com sucesso
                - `401 Unauthorized`: token ausente, inválido ou sem claim `role`
                - `403 Forbidden`: usuário autenticado não é Admin, ou o alvo pertence a outro tenant (RNF-007)
                - `404 Not Found`: usuário não encontrado
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
        CancellationToken ct)
    {
        if (principal.RequireAdminRole(out var tenantId) is { } err) return err;

        await using var uow = uowFactory.Create();

        var user = await userRepository.FindAsync(id, ct);

        if (user is null) return Results.NotFound();
        if (user.TenantId != tenantId) return Results.Forbid();

        await userRepository.RemoveAsync(user, ct);
        await uow.CommitAsync(ct);

        return Results.NoContent();
    }
}
