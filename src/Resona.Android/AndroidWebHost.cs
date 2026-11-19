using System.Net.NetworkInformation;
using System.Net.Sockets;

using Android.Util;

using Resona.UI;

namespace Resona.Android;

internal static class AndroidWebHost
{
    private const string LogTag = "Resona";

    public static void Start(int port)
    {
        Log.Info(LogTag, $"Starting web application on port {port}; open {DescribeUrls(port)}");

        // Binds all interfaces so other devices on the network can reach it
        _ = ResonaBootstrapper.StartWebHost(port, CancellationToken.None);
    }

    private static string DescribeUrls(int port)
    {
        try
        {
            var urls = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => $"http://{a.Address}:{port}/")
                .ToList();

            return urls.Count > 0 ? string.Join(", ", urls) : $"http://<device-ip>:{port}/ (no network address found)";
        }
        catch (Exception ex)
        {
            return $"http://<device-ip>:{port}/ (address lookup failed: {ex.Message})";
        }
    }
}
