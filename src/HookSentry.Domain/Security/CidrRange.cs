using System.Net;

namespace HookSentry.Domain.Security;

/// <summary>
/// Immutable IPv4 CIDR block. Tests whether a 32-bit address falls inside the range.
/// Used by <see cref="BlockedIpPolicy"/> to reject private/reserved network targets.
/// </summary>
public readonly struct CidrRange
{
    private readonly uint _network;
    private readonly uint _mask;

    public CidrRange(string baseAddress, int prefixLength)
    {
        if (prefixLength is < 0 or > 32)
            throw new ArgumentOutOfRangeException(
                nameof(prefixLength), "Prefix length must be between 0 and 32.");

        _mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        _network = ToUInt(IPAddress.Parse(baseAddress)) & _mask;
    }

    public bool Contains(uint address) => (address & _mask) == _network;

    public static uint ToUInt(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }
}
