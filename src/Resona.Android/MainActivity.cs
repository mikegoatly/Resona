using Android.Content.PM;
using Android.Views;

using Avalonia.Android;

namespace Resona.Android;

[Activity(
    Label = "Resona",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public class MainActivity : AvaloniaMainActivity
{
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
