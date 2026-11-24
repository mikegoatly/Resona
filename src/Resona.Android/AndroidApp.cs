using Android.Runtime;
using Android.Util;

using Avalonia;
using Avalonia.Android;

using Resona.Android.Platform;
using Resona.Services;
using Resona.UI;
using Resona.Android.Updates;

[assembly: global::Android.App.UsesPermission(global::Android.Manifest.Permission.Internet)]
// Needed to enumerate launcher apps (package visibility, API 30+) so the kiosk can hide them
[assembly: global::Android.App.UsesPermission("android.permission.QUERY_ALL_PACKAGES")]
[assembly: global::Android.App.UsesPermission(global::Android.Manifest.Permission.ReceiveBootCompleted)]
[assembly: global::Android.App.UsesPermission(global::Android.Manifest.Permission.BluetoothScan, UsesPermissionFlags = "neverForLocation")]
[assembly: global::Android.App.UsesPermission(global::Android.Manifest.Permission.BluetoothConnect)]
[assembly: global::Android.App.UsesPermission(global::Android.Manifest.Permission.Bluetooth, MaxSdkVersion = 30)]
[assembly: global::Android.App.UsesPermission(global::Android.Manifest.Permission.BluetoothAdmin, MaxSdkVersion = 30)]
[assembly: global::Android.App.UsesPermission(global::Android.Manifest.Permission.AccessFineLocation, MaxSdkVersion = 30)]
[assembly: global::Android.App.UsesFeature("android.hardware.bluetooth", Required = false)]

namespace Resona.Android;

/// <summary>
/// The Android application class. It is created once per process, before any activity, which makes
/// it the place to initialize Resona's services.
/// </summary>
[Application(NetworkSecurityConfig = "@xml/network_security_config")]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    private const int WebPort = 8080;

    protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CreateAppBuilder()
    {
        this.InitializeResona();
        return ResonaBootstrapper.ConfigureAppBuilder(base.CreateAppBuilder());
    }

    // CreateAppBuilder runs once per process, so this does too
    private void InitializeResona()
    {
        var options = new ResonaOptions
        {
            LibraryRoot = (this.GetExternalFilesDir(null) ?? this.FilesDir)!.AbsolutePath,
            // The app directory is read-only on Android, so custom icons live in the app's private data directory
            CustomImageDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            // Android has no other UI for toggling this, so default the web client to on
            HostWebClientDefault = true,
            EnsureLibraryFolders = true,
        };
        Log.Info(AndroidLog.Tag, $"Library root: {options.LibraryRoot}");

        LoadBass();
        ResonaBootstrapper.Initialize(options, services => services.AddAndroidPlatformServices(this));

        if (Settings.Default.HostWebClient)
        {
            AndroidWebHost.Start(WebPort);
        }

        UpdateService.Start(this);
    }

    // BASS must be loaded through the Java loader so that its JNI_OnLoad runs (it needs the JavaVM for the
    // Android media codecs); ManagedBass's P/Invokes then resolve to the already loaded library.
    private static void LoadBass()
    {
        try
        {
            Java.Lang.JavaSystem.LoadLibrary("bass");
            Log.Info(AndroidLog.Tag, "Loaded libbass.so");
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Could not load libbass.so; audio playback will not work: {ex}");
        }
    }
}
