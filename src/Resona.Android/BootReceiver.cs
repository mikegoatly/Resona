using Android.App;
using Android.Content;
using Android.Util;

namespace Resona.Android;

/// <summary>
/// Re-applies the kiosk policy (lock task packages, hidden apps, restrictions) as soon as the device has booted,
/// so it is in force before the UI is interactive. Only does anything when kiosk is active.
/// </summary>
[BroadcastReceiver(Name = "com.resona.BootReceiver", Exported = true, Enabled = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != Intent.ActionBootCompleted)
        {
            return;
        }

        try
        {
            KioskPolicy.ApplyIfActive(context);
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Boot: applying kiosk policy failed: {ex}");
        }
    }
}
