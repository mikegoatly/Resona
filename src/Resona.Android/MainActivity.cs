using Android.App;
using Android.App.Admin;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;

using Avalonia.Android;

using Resona.Android.Platform;

namespace Resona.Android;

[Activity(
    Name = KioskPolicy.MainActivityClass,
    Label = "Resona",
    LaunchMode = LaunchMode.SingleTask,
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // No-op unless this app is the device owner and kiosk mode hasn't been disabled over adb
        try
        {
            KioskPolicy.ApplyIfActive(this);
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Kiosk: applying policy failed: {ex}");
        }

        try
        {
            // Silent when device owner (the kiosk hides Settings); otherwise a normal permission prompt
            BluetoothPermissions.GrantIfDeviceOwner(this);
            BluetoothPermissions.RequestIfMissing(this);
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Bluetooth: permission setup failed: {ex}");
        }

        // Cover the display cutout so the window background (black) shows there rather than a letterbox bar
        if (OperatingSystem.IsAndroidVersionAtLeast(28) && this.Window?.Attributes is { } attributes)
        {
            attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
        }

        base.OnCreate(savedInstanceState);

        // Whatever Avalonia leaves uncovered (e.g. the cutout strip) shows the decor view, which is white by default
        this.Window?.DecorView.SetBackgroundColor(global::Android.Graphics.Color.Black);

        // As early as possible: the UI must not be interactive before lock task is engaged
        this.EnsureLockTask("OnCreate");
    }

    protected override void OnResume()
    {
        base.OnResume();
        this.EnsureLockTask("OnResume");
    }

    private void EnsureLockTask(string stage)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(23) && KioskPolicy.IsActive(this))
            {
                var manager = (ActivityManager)this.GetSystemService(ActivityService)!;
                if (manager.LockTaskModeState == LockTaskMode.None)
                {
                    Log.Info(AndroidLog.Tag, $"Kiosk: starting lock task ({stage})");
                    this.StartLockTask();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Kiosk: starting lock task failed at {stage}: {ex}");
        }
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);

        if (hasFocus)
        {
            this.HideSystemBars();
        }
    }

    // Kiosk-style immersive mode: bars stay hidden, a swipe from the edge shows them transiently.
    // Avalonia still reports display cutout insets as the safe area, which MainSingleView honours.
    private void HideSystemBars()
    {
        var window = this.Window;
        if (window is null)
        {
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var controller = window.InsetsController;
            if (controller is not null)
            {
                controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                controller.Hide(WindowInsets.Type.SystemBars());
            }
        }
    }
}
