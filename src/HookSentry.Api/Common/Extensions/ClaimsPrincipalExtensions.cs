using HookSentry.Domain.Users;
using System.Security.Claims;
using System.Security.Principal;

namespace HookSentry.Api.Common.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static IResult? RequireTenantId(this ClaimsPrincipal principal, out Guid tenantId)
    {
        if (!Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId))
            return Results.Unauthorized();
        return null;
    }

    public static IResult? RequireAdminRole(this ClaimsPrincipal principal, out Guid tenantId)
    {

        if (!Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId))
            return Results.Unauthorized();
        if (!Enum.TryParse<UserRole>(principal.TryFindRole(), out var role))
            return Results.Forbid();
        if (role != UserRole.Admin && role != UserRole.Owner)
            return Results.Forbid();
        return null;
    }

    public static UserRole GetUserRole(this ClaimsPrincipal principal)
    {
        if (Enum.TryParse<UserRole>(principal.TryFindRole(), out var role))
            return role;
        throw new ArgumentException("Invalid user role claim.");
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirstValue("email");

    private static string? TryFindRole(this ClaimsPrincipal principal)
    {
        string? role = principal.FindFirst("role")?.Value;
        if (role != null)
            return role;

        return principal.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;
    }
}
