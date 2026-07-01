using System.Net;
using HookSentry.Domain.Security;

namespace HookSentry.Tests.Security;

public class BlockedIpPolicyTests
{
    private static readonly BlockedIpPolicy Policy = new();

    public class MetodoIsBlocked
    {
        [Theory]
        [InlineData("169.254.169.254")] // cloud metadata
        [InlineData("169.254.0.1")]     // link-local
        [InlineData("127.0.0.1")]       // loopback
        [InlineData("10.0.0.5")]        // RFC 1918
        [InlineData("172.16.0.1")]      // RFC 1918
        [InlineData("172.31.255.255")]  // RFC 1918 upper bound
        [InlineData("192.168.1.1")]     // RFC 1918
        [InlineData("100.64.0.1")]      // CGNAT
        [InlineData("0.0.0.0")]         // unspecified
        [InlineData("255.255.255.255")] // broadcast
        [InlineData("224.0.0.1")]       // multicast
        public void Deve_Bloquear_Enderecos_Internos_IPv4(string ip)
        {
            Assert.True(Policy.IsBlocked(IPAddress.Parse(ip)));
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("1.1.1.1")]
        [InlineData("203.0.113.10")]
        [InlineData("172.32.0.1")]   // just outside 172.16.0.0/12
        [InlineData("11.0.0.1")]     // just outside 10.0.0.0/8
        public void Nao_Deve_Bloquear_Enderecos_Publicos_IPv4(string ip)
        {
            Assert.False(Policy.IsBlocked(IPAddress.Parse(ip)));
        }

        [Theory]
        [InlineData("::1")]                    // loopback
        [InlineData("fe80::1")]                // link-local
        [InlineData("fc00::1")]                // unique-local
        [InlineData("fd00::1")]                // unique-local
        [InlineData("ff02::1")]                // multicast
        [InlineData("::ffff:169.254.169.254")] // IPv4-mapped metadata
        [InlineData("::ffff:127.0.0.1")]       // IPv4-mapped loopback
        public void Deve_Bloquear_Enderecos_Internos_IPv6(string ip)
        {
            Assert.True(Policy.IsBlocked(IPAddress.Parse(ip)));
        }

        [Theory]
        [InlineData("2606:4700:4700::1111")] // Cloudflare public DNS
        [InlineData("2001:4860:4860::8888")] // Google public DNS
        public void Nao_Deve_Bloquear_Enderecos_Publicos_IPv6(string ip)
        {
            Assert.False(Policy.IsBlocked(IPAddress.Parse(ip)));
        }
    }
}
