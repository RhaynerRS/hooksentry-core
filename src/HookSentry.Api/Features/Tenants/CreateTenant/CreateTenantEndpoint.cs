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
            .WithSummary("Registers a new tenant with an initial admin user")
            .WithDescription("""
                Creates a new tenant and its first admin user in a single atomic operation.
                Automatically generates the `webhook_secret` (HMAC-SHA256).

                **No authentication required.**

                **Body:**
                - `name` *(required)*: unique organization name
                - `adminEmail` *(required)*: initial admin user email — unique on the platform
                - `adminPassword` *(required)*: admin password — stored as hash
                - `maxTrys` *(optional, default: 10)*: maximum number of attempts before DLQ
                - `circuitBreakerTimer` *(optional, default: 300)*: duration in seconds of the Circuit Breaker OPEN state

                **Return codes:**
                - `201 Created`: tenant and admin created — includes the generated `webhookSecret` and admin data
                - `400 Bad Request`: invalid data (malformed email, empty password)
                - `409 Conflict`: a tenant with the same name already exists, or the email is already in use
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
            return Results.Conflict($"Email '{request.AdminEmail}' is already in use.");

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
