using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HookSentry.Infrastructure.AbuseProtection;

public sealed class RedisFingerprintGuard(
    IConnectionMultiplexer redis,
    IOptions<RegistrationAbuseOptions> options) : IFingerprintGuard
{
    public async Task<FingerprintCheckResult> CheckAsync(string fingerprint, CancellationToken ct)
    {
        try
        {
            var key   = FpKey(fingerprint);
            var count = (long)await redis.GetDatabase().SetLengthAsync(key);
            var limit = options.Value.MaxAccountsPerFingerprint;
            return new FingerprintCheckResult(count >= limit, (int)count, limit);
        }
        catch
        {
            return new FingerprintCheckResult(false, 0, options.Value.MaxAccountsPerFingerprint);
        }
    }

    public async Task RecordAsync(string fingerprint, Guid tenantId, CancellationToken ct)
    {
        try
        {
            var key = FpKey(fingerprint);
            var db  = redis.GetDatabase();
            await db.SetAddAsync(key, tenantId.ToString());
            await db.KeyExpireAsync(key, TimeSpan.FromDays(options.Value.FingerprintBlockWindowDays));
        }
        catch { /* fail open — registration already succeeded */ }
    }

    private static string FpKey(string fp)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fp));
        return $"abuse:fp:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
