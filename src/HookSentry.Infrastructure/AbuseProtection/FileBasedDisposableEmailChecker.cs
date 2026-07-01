using Microsoft.Extensions.Hosting;

namespace HookSentry.Infrastructure.AbuseProtection;

public sealed class FileBasedDisposableEmailChecker : IDisposableEmailChecker
{
    private readonly HashSet<string> _blocklist;

    public FileBasedDisposableEmailChecker(IHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Resources", "disposable-domains.txt");
        _blocklist = File.Exists(path)
            ? [.. File.ReadAllLines(path)
                  .Select(l => l.Trim().ToLowerInvariant())
                  .Where(l => l.Length > 0 && !l.StartsWith('#'))]
            : [];
    }

    public Task<bool> IsDisposableAsync(string email, CancellationToken ct)
    {
        var domain = email.Split('@').LastOrDefault()?.ToLowerInvariant() ?? "";
        return Task.FromResult(_blocklist.Contains(domain));
    }
}
