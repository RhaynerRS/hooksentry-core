using System.Security.Claims;
using HookSentry.Domain.Users;

namespace HookSentry.Api.Common.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static IResult? RequireTenantId(this ClaimsPrincipal principal, out Guid tenantId)
    {
        if (!Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId))
            return Results.Unauthorized();
        return null;
    }

    // Accepts Owner (10) and Admin (1) — both are management-level roles in the cloud.
    public static IResult? RequireAdminRole(this ClaimsPrincipal principal, out Guid tenantId)
    {
        if (!Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId))
            return Results.Unauthorized();
        if (!Enum.TryParse<UserRole>(principal.FindFirst("role")?.Value, out var role))
            return Results.Forbid();
        if (role != UserRole.Admin && role != UserRole.Owner)
            return Results.Forbid();
        return null;
    }

    public static UserRole GetUserRole(this ClaimsPrincipal principal)
    {
        if (Enum.TryParse<UserRole>(principal.FindFirst("role")?.Value, out var role))
            return role;
        throw new ArgumentException("Invalid user role claim.");
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirstValue("email");
}
