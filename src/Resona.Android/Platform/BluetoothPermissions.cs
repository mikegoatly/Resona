using Android.App;
using Android.App.Admin;
using Android.Content;
using Android.Content.PM;
using Android.Util;

namespace Resona.Android.Platform;

/// <summary>
/// The runtime permissions Bluetooth pairing needs. The kiosk blocks the Settings app, so as device owner they are
/// granted silently; otherwise they are requested through the activity.
/// </summary>
internal static class BluetoothPermissions
{
    public const int RequestCode = 4711;

    public static string[] Required()
        => OperatingSystem.IsAndroidVersionAtLeast(31)
            ? [global::Android.Manifest.Permission.BluetoothScan, global::Android.Manifest.Permission.BluetoothConnect]
            // Before Android 12, discovery only returns results when location permission is held
            : [global::Android.Manifest.Permission.AccessFineLocation];

    public static string[] Missing(Context context)
        => Required().Where(p => context.CheckSelfPermission(p) != Permission.Granted).ToArray();

    public static bool HasAll(Context context) => Missing(context).Length == 0;

    /// <summary>When device owner, grants any missing permission without a prompt. No-op otherwise.</summary>
    public static void GrantIfDeviceOwner(Context context)
    {
        if (!KioskPolicy.IsDeviceOwner(context))
        {
            return;
        }

        var dpm = KioskPolicy.GetManager(context);
        var admin = ResonaDeviceAdminReceiver.GetComponent(context);
        foreach (var permission in Missing(context))
        {
            try
            {
                var ok = dpm.SetPermissionGrantState(admin, context.PackageName!, permission, PermissionGrantState.Granted);
                Log.Info(AndroidLog.Tag, $"Bluetooth: granted {permission} as device owner: {ok}");
            }
            catch (Exception ex)
            {
                Log.Error(AndroidLog.Tag, $"Bluetooth: granting {permission} failed: {ex.Message}");
            }
        }
    }

    /// <summary>Asks the user for any missing permission; for non device owners.</summary>
    public static void RequestIfMissing(Activity activity)
    {
        var missing = Missing(activity);
        if (missing.Length == 0 || KioskPolicy.IsDeviceOwner(activity))
        {
            return;
        }

        Log.Info(AndroidLog.Tag, $"Bluetooth: requesting {string.Join(", ", missing)}");
        activity.RequestPermissions(missing, RequestCode);
    }
}
