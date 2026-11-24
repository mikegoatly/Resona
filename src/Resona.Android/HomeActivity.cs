using Android.App;
using Android.Content;
using Android.OS;

namespace Resona.Android;

/// <summary>
/// The kiosk HOME entry point. It is disabled in the manifest so that non-kiosk installs are unaffected;
/// <see cref="KioskPolicy"/> enables it and makes it the persistent preferred HOME activity. It only forwards to
/// <see cref="MainActivity"/> (Avalonia's single view can't be hosted by two activity instances).
/// </summary>
[Activity(
    Name = KioskPolicy.HomeActivityClass,
    Label = "Resona",
    Enabled = false,
    Exported = true,
    NoHistory = true,
    ExcludeFromRecents = true,
    Theme = "@android:style/Theme.NoDisplay")]
[IntentFilter(
    new[] { Intent.ActionMain },
    Categories = new[] { Intent.CategoryHome, Intent.CategoryDefault })]
public class HomeActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        KioskPolicy.LaunchMainActivity(this);
        this.Finish();
    }
}
