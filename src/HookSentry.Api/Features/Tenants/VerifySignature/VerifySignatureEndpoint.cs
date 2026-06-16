using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HookSentry.Api.Common.Endpoints;
using HookSentry.Api.Common.Extensions;
using HookSentry.Api.DataTransfer.Tenants.Requests;
using HookSentry.Api.DataTransfer.Tenants.Responses;
using HookSentry.Domain.Tenants;

namespace HookSentry.Api.Features.Tenants.VerifySignature;

public class VerifySignatureEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/tenants/{id:guid}/webhook-secret/verify", Handle)
            .WithName("VerifyWebhookSignature")
            .WithTags("Tenants")
            .WithSummary("Verifies a webhook signature against the tenant secret")
            .WithDescription("""
                Computes HMAC-SHA256 of the provided payload using the tenant's webhook secret
                and compares it (timing-safe) against the provided signature.

                **Route parameters:**
                - `id` *(required)*: tenant UUID

                **Body:**
                - `payload` *(required)*: raw JSON string that was signed
                - `signature` *(required)*: value of the X-HookSentry-Signature header (e.g. `sha256=abc123...`)

                **Return codes:**
                - `200 OK`: `{ "valid": true | false }`
                - `401 Unauthorized`: missing or invalid token
                - `403 Forbidden`: tenant belongs to another user
                - `404 Not Found`: tenant not found
                """)
            .RequireAuthorization()
            .Produces<VerifySignatureResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        Guid id,
        VerifySignatureRequest body,
        ClaimsPrincipal user,
        ITenantRepository tenantRepository,
        CancellationToken ct)
    {
        if (user.RequireTenantId(out var tenantId) is { } err) return err;
        if (tenantId != id) return Results.Forbid();

        var tenant = await tenantRepository.FindAsync(id, ct);
        if (tenant is null) return Results.NotFound();

        var keyBytes  = Encoding.UTF8.GetBytes(tenant.WebhookSecret);
        var dataBytes = Encoding.UTF8.GetBytes(body.Payload);
        var hash      = HMACSHA256.HashData(keyBytes, dataBytes);
        var expected  = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        var valid = body.Signature.Length == expected.Length &&
                    CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(body.Signature),
                        Encoding.UTF8.GetBytes(expected));

        return Results.Ok(new VerifySignatureResponse(valid));
    }
}
