using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Validation;
using HookSentry.Domain;
using HookSentry.Domain.Security;
using HookSentry.Api.DataTransfer.Tenants.Requests;
using HookSentry.Api.DataTransfer.Tenants.Responses;
using HookSentry.Domain.Tenants;
using HookSentry.Domain.Users;

namespace HookSentry.Api.Features.Tenants.CreateTenant;

public class CreateTenantEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/tenants", Handle)
            .WithName("CreateTenant")
            .WithTags("Tenants")
            .WithSummary("Cadastra um novo tenant com usuário admin inicial")
            .WithDescription("""
                Cria um novo tenant e seu primeiro usuário administrador em uma única operação atômica.
                Gera automaticamente o `webhook_secret` (HMAC-SHA256).

                **Não requer autenticação.**

                **Body:**
                - `name` *(obrigatório)*: nome único da organização
                - `adminEmail` *(obrigatório)*: e-mail do usuário administrador inicial — único na plataforma
                - `adminPassword` *(obrigatório)*: senha do administrador — armazenada como hash
                - `maxTrys` *(opcional, padrão: 10)*: número máximo de tentativas antes da DLQ
                - `circuitBreakerTimer` *(opcional, padrão: 300)*: duração em segundos do estado OPEN do Circuit Breaker

                **Códigos de retorno:**
                - `201 Created`: tenant e admin criados — inclui o `webhookSecret` gerado e dados do admin
                - `400 Bad Request`: dados inválidos (e-mail mal formatado, senha vazia)
                - `409 Conflict`: já existe um tenant com o mesmo nome, ou o e-mail já está em uso
                """)
            .AllowAnonymous()
            .Produces<CreateTenantResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> Handle(
        CreateTenantRequest request,
        IPasswordHasher passwordHasher,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IUnitOfWorkFactory uowFactory,
        CancellationToken ct)
    {
        if (InputSanitizer.ValidateName(request.Name) is { } nameErr)
            return Results.BadRequest(nameErr);
        if (InputSanitizer.ValidateEmail(request.AdminEmail) is { } emailErr)
            return Results.BadRequest(emailErr);

        if (await tenantRepository.NameExistsAsync(request.Name, ct))
            return Results.Conflict($"Tenant '{request.Name}' already exists.");

        var normalizedEmail = request.AdminEmail.Trim().ToLowerInvariant();

        if (await userRepository.EmailExistsAsync(normalizedEmail, ct))
            return Results.Conflict($"E-mail '{request.AdminEmail}' já está em uso.");

        Tenant tenant;
        try { tenant = new Tenant(request.Name, request.MaxTrys, request.CircuitBreakerTimer); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        string passwordHash;
        try { passwordHash = passwordHasher.Hash(request.AdminPassword); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        User admin;
        try { admin = new User(tenant.Id, normalizedEmail, passwordHash, UserRole.Admin); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }

        await using var uow = uowFactory.Create();
        await tenantRepository.AddAsync(tenant, ct);
        await userRepository.AddAsync(admin, ct);
        await uow.CommitAsync(ct);

        return Results.Created(
            $"/api/v1/tenants/{tenant.Id}",
            new CreateTenantResponse(
                tenant.Id,
                tenant.Name,
                tenant.WebhookSecret,
                tenant.MaxTrys,
                tenant.CircuitBreakerTimer,
                tenant.CreatedAt,
                admin.Id,
                admin.Email,
                admin.Role,
                admin.CreatedAt));
    }
}
