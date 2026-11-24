using Android.App;
using Android.Content;
using Android.Util;

namespace Resona.Android;

/// <summary>
/// Installing an update kills the app and the system does not relaunch it on its own, so bring the kiosk back
/// (MainActivity re-applies the policy and starts lock task). Only does anything when kiosk is active.
/// </summary>
[BroadcastReceiver(Name = "com.resona.PackageReplacedReceiver", Exported = false)]
[IntentFilter(new[] { Intent.ActionMyPackageReplaced })]
public class PackageReplacedReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != Intent.ActionMyPackageReplaced)
        {
            return;
        }

        try
        {
            if (KioskPolicy.IsActive(context))
            {
                Log.Info(AndroidLog.Tag, "Package replaced; relaunching kiosk");
                KioskPolicy.LaunchMainActivity(context);
            }
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Relaunch after package replace failed: {ex}");
        }
    }
}
