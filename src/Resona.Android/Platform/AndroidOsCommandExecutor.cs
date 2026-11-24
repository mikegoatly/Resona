using Android.App.Admin;
using Android.Content;
using Android.OS;
using Android.Util;

using Resona.Services.OS;

namespace Resona.Android.Platform;

/// <summary>
/// Maps "shut down" to "turn the screen off" and "restart" to a device reboot. Both need device owner; without it
/// they only log, because an ordinary app cannot do either.
/// </summary>
internal sealed class AndroidOsCommandExecutor(Context context) : IOsCommandExecutor
{

    public void Shutdown()
    {
        if (!KioskPolicy.IsDeviceOwner(context))
        {
            Log.Warn(AndroidLog.Tag, "Shutdown ignored: not device owner, cannot turn the screen off");
            return;
        }

        // The inactivity timer can ask repeatedly; there is nothing to do if the screen is already off
        var power = (PowerManager)context.GetSystemService(Context.PowerService)!;
        if (!power.IsInteractive)
        {
            return;
        }

        // The kiosk disables the keyguard, so this just puts the display to sleep; a touch or power press wakes it
        Log.Info(AndroidLog.Tag, "Shutdown: turning the screen off");
        try
        {
            KioskPolicy.GetManager(context).LockNow();
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Shutdown: LockNow failed: {ex.Message}");
        }
    }

    public void Restart()
    {
        if (!KioskPolicy.IsDeviceOwner(context))
        {
            Log.Warn(AndroidLog.Tag, "Restart ignored: not device owner, cannot reboot");
            return;
        }

        Log.Info(AndroidLog.Tag, "Restart: rebooting the device");
        try
        {
            KioskPolicy.GetManager(context).Reboot(ResonaDeviceAdminReceiver.GetComponent(context));
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Restart: Reboot failed: {ex.Message}");
        }
    }
}
