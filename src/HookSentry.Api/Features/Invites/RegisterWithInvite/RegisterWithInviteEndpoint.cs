using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain;
using HookSentry.Domain.Security;
using HookSentry.Api.DataTransfer.Invites.Requests;
using HookSentry.Api.DataTransfer.Users.Responses;
using HookSentry.Domain.Invites;
using HookSentry.Domain.Users;

namespace HookSentry.Api.Features.Invites.RegisterWithInvite;

public class RegisterWithInviteEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/invites/{token}/register", Handle)
            .WithName("RegisterWithInvite")
            .WithTags("Invites")
            .WithSummary("Cadastra um usuário usando um link de convite")
            .WithDescription("""
                Registra um novo usuário Developer no tenant associado ao convite.
                O convite é marcado como utilizado após o cadastro bem-sucedido.

                **Não requer autenticação.**

                **Parâmetros de rota:**
                - `token` *(obrigatório)*: token gerado pelo admin ao criar o convite

                **Body:**
                - `email` *(obrigatório)*: endereço de e-mail único na plataforma — máx. 255 caracteres
                - `password` *(obrigatório)*: senha em texto plano — será armazenada como hash

                **Códigos de retorno:**
                - `201 Created`: usuário criado com role Developer
                - `400 Bad Request`: dados inválidos (e-mail mal formatado, senha vazia)
                - `404 Not Found`: token de convite não encontrado
                - `409 Conflict`: convite já utilizado ou expirado; ou e-mail já cadastrado na plataforma
                """)
            .AllowAnonymous()
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> Handle(
        string token,
        RegisterWithInviteRequest request,
        IPasswordHasher passwordHasher,
        IInviteTokenRepository inviteRepository,
        IUserRepository userRepository,
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        var invite = await inviteRepository.FindByTokenAsync(token, ct);

        if (invite is null)
            return Results.NotFound("Token de convite não encontrado.");

        if (InputSanitizer.ValidateEmail(request.Email) is { } emailErr)
            return Results.BadRequest(emailErr);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await userRepository.EmailExistsAsync(normalizedEmail, ct))
            return Results.Conflict($"E-mail '{request.Email}' já está em uso.");

        string passwordHash;
        try { passwordHash = passwordHasher.Hash(request.Password); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        await using var uow = uowFactory.Create();

        try { invite.Use(); }
        catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }

        User newUser;
        try { newUser = new User(invite.TenantId, request.Email, passwordHash, UserRole.Developer); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        await userRepository.AddAsync(newUser, ct);
        await uow.CommitAsync(ct);

        return Results.Created($"/api/v1/users/{newUser.Id}", UserResponse.From(newUser));
    }
}
