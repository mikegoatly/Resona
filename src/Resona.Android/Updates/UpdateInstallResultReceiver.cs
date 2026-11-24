using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Util;

namespace Resona.Android.Updates;

/// <summary>Receives the PackageInstaller session result (not exported; targeted by an explicit PendingIntent).</summary>
[BroadcastReceiver(Name = "com.resona.UpdateInstallResultReceiver", Exported = false)]
public class UpdateInstallResultReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        var status = (PackageInstallStatus)intent.GetIntExtra(PackageInstaller.ExtraStatus, -1);
        var message = intent.GetStringExtra(PackageInstaller.ExtraStatusMessage);
        Log.Info(AndroidLog.Tag, $"Update: install result {status}: {message}");

        if (status == PackageInstallStatus.PendingUserAction)
        {
            // Should not happen for a device owner; do not surface a prompt on a kiosk
            Log.Warn(AndroidLog.Tag, "Update: installer wants user action; abandoning");
            UpdatePrefs.RecordResult(context, "install-needs-user-action");
        }
        else if (status != PackageInstallStatus.Success)
        {
            UpdatePrefs.RecordResult(context, $"install-failed:{status}:{message}");
        }
        // On success the process is killed by the system as the package is replaced.
    }
}
