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
            .WithSummary("Atualiza parcialmente um usuário")
            .WithDescription("""
                Atualiza campos de um usuário pertencente ao tenant autenticado (RNF-007).
                Apenas campos enviados no body são alterados — os demais permanecem inalterados.

                **Parâmetros de rota:**
                - `id` *(obrigatório)*: UUID do usuário

                **Body** *(todos os campos são opcionais):*
                - `email`: novo endereço de e-mail único na plataforma (RN-001)
                - `password`: nova senha em texto plano — será armazenada como hash
                - `role`: novo perfil — `0` = Developer, `1` = Admin
                - `status`: novo status — `0` = Active, `1` = Inactive

                **Códigos de retorno:**
                - `200 OK`: usuário atualizado com sucesso
                - `400 Bad Request`: valor inválido (e-mail mal formatado, role fora do domínio)
                - `401 Unauthorized`: token ausente ou inválido
                - `403 Forbidden`: usuário pertence a outro tenant (RNF-007)
                - `404 Not Found`: usuário não encontrado
                - `409 Conflict`: e-mail já está em uso por outro usuário (RN-001)
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
                return Results.Conflict($"E-mail '{request.Email}' já está em uso.");
        }

        try
        {
            if (request.Email is not null) user.SetEmail(request.Email);
            if (request.Password is not null) user.SetPasswordHash(passwordHasher.Hash(request.Password));
            if (request.Role is not null && principal.GetUserRole() == UserRole.Admin) user.SetRole(request.Role.Value);
            if (request.Status == UserStatus.Active) user.Activate();
            else if (request.Status == UserStatus.Inactive) user.Deactivate();
        }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        await uow.CommitAsync(ct);
        return Results.Ok(UserResponse.From(user));
    }
}
