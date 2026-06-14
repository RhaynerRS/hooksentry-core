using System.Security.Claims;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain;
using HookSentry.Domain.Security;
using HookSentry.Api.DataTransfer.Users.Requests;
using HookSentry.Api.DataTransfer.Users.Responses;
using HookSentry.Domain.Tenants;
using HookSentry.Domain.Users;

namespace HookSentry.Api.Features.Users.CreateUser;

public class CreateUserEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/users", Handle)
            .WithName("CreateUser")
            .WithTags("Users")
            .WithSummary("Cria um novo usuário no tenant autenticado")
            .WithDescription("""
                Cria um novo usuário vinculado ao tenant do token JWT.
                A senha é armazenada como hash — nunca em texto plano (RNF-008).

                **Body:**
                - `email` *(obrigatório)*: endereço de e-mail único na plataforma — máx. 255 caracteres (RN-001)
                - `password` *(obrigatório)*: senha em texto plano — será armazenada como hash
                - `role` *(opcional, padrão: `0` = Developer)*: perfil de acesso
                  - `0` = Developer: gerencia URLs, API Keys e visualiza eventos do tenant
                  - `1` = Admin: tudo do Developer + purga de fila e gerenciamento de usuários (RF-014)

                **Códigos de retorno:**
                - `201 Created`: usuário criado com sucesso
                - `400 Bad Request`: dados inválidos (e-mail mal formatado, senha vazia)
                - `401 Unauthorized`: token ausente ou inválido
                - `404 Not Found`: tenant não encontrado
                - `409 Conflict`: já existe um usuário com este e-mail (RN-001)
                """)
            .RequireAuthorization()
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> Handle(
        CreateUserRequest request,
        ClaimsPrincipal principal,
        IPasswordHasher passwordHasher,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        if (principal.RequireTenantId(out var tenantId) is { } err) return err;

        if (InputSanitizer.ValidateEmail(request.Email) is { } emailErr)
            return Results.BadRequest(emailErr);

        var tenant = await tenantRepository.FindAsync(tenantId, ct);
        if (tenant is null)
            return Results.NotFound($"Tenant '{tenantId}' not found.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await userRepository.EmailExistsAsync(normalizedEmail, ct))
            return Results.Conflict($"E-mail '{request.Email}' já está em uso.");

        string passwordHash;
        try { passwordHash = passwordHasher.Hash(request.Password); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        User newUser;
        try { newUser = new User(tenantId, request.Email, passwordHash, request.Role); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        await using var uow = uowFactory.Create();
        await userRepository.AddAsync(newUser, ct);
        await uow.CommitAsync(ct);

        return Results.Created($"/api/v1/users/{newUser.Id}", UserResponse.From(newUser));
    }
}
