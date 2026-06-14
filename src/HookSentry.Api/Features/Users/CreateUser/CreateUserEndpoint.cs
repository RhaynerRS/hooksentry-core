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
            .WithSummary("Creates a new user in the authenticated tenant")
            .WithDescription("""
                Creates a new user linked to the tenant from the JWT token.
                The password is stored as a hash — never in plain text (RNF-008).

                **Body:**
                - `email` *(required)*: unique email address on the platform — max 255 characters (RN-001)
                - `password` *(required)*: plain text password — will be stored as hash
                - `role` *(optional, default: `0` = Developer)*: access role
                  - `0` = Developer: manages URLs, API Keys, and views tenant events
                  - `1` = Admin: everything in Developer + queue purge and user management (RF-014)

                **Return codes:**
                - `201 Created`: user created successfully
                - `400 Bad Request`: invalid data (malformed email, empty password)
                - `401 Unauthorized`: missing or invalid token
                - `404 Not Found`: tenant not found
                - `409 Conflict`: a user with this email already exists (RN-001)
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
            return Results.Conflict($"Email '{request.Email}' is already in use.");

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
