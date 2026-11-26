using System.Net;

using Resona.Services.OS;

namespace Resona.Services.Tests
{
    public class NetworkAddressesTests
    {
        [Fact]
        public void FilterLocalIPv4_KeepsOnlyNonLoopbackIPv4Addresses()
        {
            var result = NetworkAddresses.FilterLocalIPv4(
            [
                IPAddress.Parse("192.168.1.20"),
                IPAddress.Loopback,
                IPAddress.IPv6Loopback,
                IPAddress.Parse("fe80::1"),
                IPAddress.Parse("10.0.0.5"),
            ]);

            Assert.Equal([IPAddress.Parse("192.168.1.20"), IPAddress.Parse("10.0.0.5")], result);
        }

        [Fact]
        public void FilterLocalIPv4_RemovesDuplicates()
        {
            var result = NetworkAddresses.FilterLocalIPv4(
                [IPAddress.Parse("192.168.1.20"), IPAddress.Parse("192.168.1.20")]);

            Assert.Single(result);
        }

        [Fact]
        public void FormatUrls_IncludesAddressAndPort()
        {
            var result = NetworkAddresses.FormatUrls([IPAddress.Parse("192.168.1.20")], 8080);

            Assert.Equal(["http://192.168.1.20:8080/"], result);
        }

        [Fact]
        public void FormatUrls_NoAddresses_ReturnsEmpty()
        {
            Assert.Empty(NetworkAddresses.FormatUrls([], 8080));
        }
    }
}
