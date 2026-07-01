using System.Net;
using System.Net.Sockets;
using HookSentry.Domain.Security;

namespace HookSentry.Infrastructure.Security;

/// <summary>
/// Builds SSRF-hardened <see cref="HttpClient"/> instances. The connection is
/// established through a custom <see cref="SocketsHttpHandler.ConnectCallback"/> that
/// resolves the host and refuses to connect if <em>any</em> resolved address is
/// blocked by <see cref="BlockedIpPolicy"/>. Validating at connect time (rather than
/// before the request) closes the DNS-rebinding / TOCTOU gap and also covers the
/// target of any redirect — though auto-redirect is disabled as defence in depth.
/// TLS is still applied by the handler on top of the returned stream.
/// </summary>
public sealed class SafeHttpClientFactory(BlockedIpPolicy blockedIps) : ISafeHttpClientFactory
{
    public HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = ConnectGuardedAsync,
        };

        return new HttpClient(handler, disposeHandler: true);
    }

    private async ValueTask<Stream> ConnectGuardedAsync(
        SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0)
            throw new OutboundUrlBlockedException($"Host '{host}' did not resolve to any address.");

        foreach (var address in addresses)
            if (blockedIps.IsBlocked(address))
                throw new OutboundUrlBlockedException(
                    $"Refusing to connect to '{host}': resolves to a disallowed address ({address}).");

        return await OpenStreamAsync(addresses[0], port, ct);
    }

    private static async ValueTask<Stream> OpenStreamAsync(IPAddress address, int port, CancellationToken ct)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
