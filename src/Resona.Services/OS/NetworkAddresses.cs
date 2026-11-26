using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using Serilog;

namespace Resona.Services.OS
{
    /// <summary>Finds the addresses other devices on the local network can use to reach this device.</summary>
    public static class NetworkAddresses
    {
        private static readonly ILogger logger = Log.ForContext(typeof(NetworkAddresses));

        /// <summary>
        /// The IPv4 addresses of the network interfaces that are up, excluding loopback. Returns an empty list if
        /// the lookup fails (e.g. the platform doesn't allow enumerating interfaces).
        /// </summary>
        public static IReadOnlyList<IPAddress> GetLocalIPv4Addresses()
        {
            try
            {
                return FilterLocalIPv4(
                    NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                        .Select(a => a.Address));
            }
            catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException or InvalidOperationException)
            {
                logger.Warning(ex, "Unable to enumerate network addresses");
                return [];
            }
        }

        /// <summary>Formats the URLs the web client can be reached on, e.g. <c>http://192.168.1.20:8080/</c>.</summary>
        public static IReadOnlyList<string> GetWebClientUrls(int port) => FormatUrls(GetLocalIPv4Addresses(), port);

        internal static IReadOnlyList<IPAddress> FilterLocalIPv4(IEnumerable<IPAddress> addresses) =>
            addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                .Distinct()
                .ToList();

        internal static IReadOnlyList<string> FormatUrls(IEnumerable<IPAddress> addresses, int port) =>
            addresses.Select(a => $"http://{a}:{port}/").ToList();
    }
}
