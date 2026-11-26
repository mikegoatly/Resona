using Android.Util;

using Resona.Services.OS;
using Resona.UI;

namespace Resona.Android;

internal static class AndroidWebHost
{

    public static void Start(int port)
    {
        Log.Info(AndroidLog.Tag, $"Starting web application on port {port}; open {DescribeUrls(port)}");

        // Binds all interfaces so other devices on the network can reach it
        _ = ResonaBootstrapper.StartWebHost(port, CancellationToken.None);
    }

    private static string DescribeUrls(int port)
    {
        var urls = NetworkAddresses.GetWebClientUrls(port);
        return urls.Count > 0 ? string.Join(", ", urls) : $"http://<device-ip>:{port}/ (no network address found)";
    }
}
