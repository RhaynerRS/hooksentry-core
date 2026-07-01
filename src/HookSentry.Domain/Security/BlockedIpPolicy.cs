using System.Net;
using System.Net.Sockets;

namespace HookSentry.Domain.Security;

/// <summary>
/// Pure policy that decides whether an outbound target IP must be refused to
/// prevent SSRF. Blocks loopback, private (RFC 1918), link-local — including the
/// cloud metadata address 169.254.169.254 — CGNAT, multicast and reserved ranges,
/// for both IPv4 and IPv6. Contains no I/O so it is fully unit-testable.
/// </summary>
public sealed class BlockedIpPolicy
{
    private static readonly IReadOnlyList<CidrRange> BlockedV4 =
    [
        new("0.0.0.0", 8),       // "this host" / unspecified
        new("10.0.0.0", 8),      // RFC 1918 private
        new("100.64.0.0", 10),   // RFC 6598 CGNAT
        new("127.0.0.0", 8),     // loopback
        new("169.254.0.0", 16),  // link-local (incl. 169.254.169.254 cloud metadata)
        new("172.16.0.0", 12),   // RFC 1918 private
        new("192.0.0.0", 24),    // IETF protocol assignments
        new("192.168.0.0", 16),  // RFC 1918 private
        new("198.18.0.0", 15),   // benchmarking
        new("224.0.0.0", 4),     // multicast
        new("240.0.0.0", 4),     // reserved + 255.255.255.255 broadcast
    ];

    public bool IsBlocked(IPAddress address)
    {
        var ip = Normalize(address);

        return ip.AddressFamily == AddressFamily.InterNetwork
            ? IsBlockedV4(ip)
            : IsBlockedV6(ip);
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool IsBlockedV4(IPAddress ip)
    {
        var value = CidrRange.ToUInt(ip);

        foreach (var range in BlockedV4)
            if (range.Contains(value))
                return true;

        return false;
    }

    private static bool IsBlockedV6(IPAddress ip) =>
        IPAddress.IsLoopback(ip)
        || ip.Equals(IPAddress.IPv6Any)
        || ip.IsIPv6LinkLocal
        || ip.IsIPv6SiteLocal
        || ip.IsIPv6UniqueLocal
        || ip.IsIPv6Multicast
        || ip.IsIPv6Teredo;
}
