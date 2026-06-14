using System.Security.Claims;
using System.Text.Encodings.Web;
using HookSentry.Domain.ApiKeys;
using HookSentry.Infrastructure.ApiKeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NHibernate;
using NHibernate.Linq;

namespace HookSentry.Api.Common.Auth;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyCacheService cache,
    ISessionFactory sessionFactory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValues))
            return AuthenticateResult.NoResult();

        var rawKey = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawKey))
            return AuthenticateResult.NoResult();

        var hash = ApiKey.ComputeHash(rawKey);

        var entry = await cache.GetAsync(hash);
        if (entry is null)
        {
            using var session = sessionFactory.OpenSession();
            var match = await session.Query<ApiKey>()
                .Where(k => k.KeyHash == hash && k.IsActive)
                .Select(k => new { k.TenantId })
                .FirstOrDefaultAsync();

            if (match is null)
                return AuthenticateResult.Fail("Invalid or revoked API key.");

            entry = new ApiKeyCacheEntry(match.TenantId);
            await cache.SetAsync(hash, entry);
        }

        var claims = new[] { new Claim("tenant_id", entry.TenantId.ToString()) };
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)),
            Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
