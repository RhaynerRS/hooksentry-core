namespace HookSentry.Infrastructure.Security;

/// <summary>
/// Creates <see cref="HttpClient"/> instances hardened against SSRF: every outbound
/// connection is refused when the resolved IP targets an internal/loopback/link-local
/// address, and automatic redirect following is disabled (redirects could otherwise
/// bounce an allowed host onto an internal one).
/// </summary>
public interface ISafeHttpClientFactory
{
    HttpClient Create();
}
