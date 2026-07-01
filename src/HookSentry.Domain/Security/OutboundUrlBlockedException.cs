namespace HookSentry.Domain.Security;

/// <summary>
/// Raised when an outbound HTTP connection is refused because the target host
/// resolves to a disallowed (internal/loopback/link-local) address. Surfaced by
/// the SSRF connect guard and treated as a delivery failure by the Worker.
/// </summary>
public sealed class OutboundUrlBlockedException : Exception
{
    public OutboundUrlBlockedException(string message) : base(message) { }
}
