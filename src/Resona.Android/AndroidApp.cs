using Android.Runtime;
using Android.Util;

using Avalonia;
using Avalonia.Android;

using Resona.Services;
using Resona.UI;

namespace Resona.Android;

/// <summary>
/// The Android application class. It is created once per process, before any activity, which makes
/// it the place to initialize Resona's services.
/// </summary>
[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    private const string LogTag = "Resona";
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
        Log.Info(LogTag, $"Library root: {options.LibraryRoot}");

        ResonaBootstrapper.Initialize(options);

        if (Settings.Default.HostWebClient)
        {
            AndroidWebHost.Start(WebPort);
        }
    }
}
