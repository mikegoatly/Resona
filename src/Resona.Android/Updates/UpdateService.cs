using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Util;

using Resona.Services.Audio;
using Resona.Services.Updates;

using Splat;

namespace Resona.Android.Updates;

/// <summary>
/// LAN self-update: polls the update URL for resona-update.json, downloads and verifies a newer APK, waits until
/// nothing is playing, then installs it silently through PackageInstaller (no prompt, because the app is device owner).
/// A single loop tied to the process; the system restarts the app after the package is replaced.
/// </summary>
internal static class UpdateService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan IdlePoll = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    private static readonly SemaphoreSlim Trigger = new(0);
    private static int started;

    public static void Start(Context context)
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
        {
            return;
        }

        var appContext = context.ApplicationContext!;
        _ = Task.Run(() => LoopAsync(appContext));
    }

    /// <summary>Requests an immediate check (also wakes the loop from its 6 hour wait).</summary>
    public static void CheckNow() => Trigger.Release();

    private static async Task LoopAsync(Context context)
    {
        await Task.Delay(InitialDelay);
        while (true)
        {
            try
            {
                await CheckCoreAsync(context);
            }
            catch (Exception ex)
            {
                Log.Warn(AndroidLog.Tag, $"Update: check failed: {ex.Message}");
                UpdatePrefs.RecordResult(context, "error:" + ex.Message);
            }

            // A CHECK_UPDATE that arrived while a check was running is covered by that check; don't re-run it now
            while (Trigger.Wait(0))
            {
            }

            await Trigger.WaitAsync(CheckInterval);
        }
    }

    private static async Task CheckCoreAsync(Context context)
    {
        var url = UpdatePrefs.GetUrl(context);
        if (url is null)
        {
            Log.Info(AndroidLog.Tag, "Update: no update source configured");
            return;
        }

        if (!KioskPolicy.IsDeviceOwner(context))
        {
            Log.Info(AndroidLog.Tag, "Update: not device owner; skipping");
            UpdatePrefs.RecordResult(context, "skipped:not-device-owner");
            return;
        }

        using var http = new HttpClient { Timeout = HttpTimeout };

        var manifestUrl = UpdateManifest.CombineUrl(url, UpdateManifest.FileName);
        Log.Info(AndroidLog.Tag, $"Update: checking {manifestUrl}");
        var json = await http.GetStringAsync(manifestUrl);
        if (!UpdateManifest.TryParse(json, out var manifest, out var error))
        {
            Log.Warn(AndroidLog.Tag, $"Update: bad manifest: {error}");
            UpdatePrefs.RecordResult(context, "bad-manifest:" + error);
            return;
        }

        var installed = GetInstalledVersionCode(context);
        if (!manifest.IsNewerThan(installed))
        {
            Log.Info(AndroidLog.Tag, $"Update: up to date (installed {installed}, available {manifest.VersionCode})");
            UpdatePrefs.RecordResult(context, $"up-to-date:{installed}");
            return;
        }

        Log.Info(AndroidLog.Tag, $"Update: {manifest.VersionCode} available (installed {installed}); downloading {manifest.Apk}");
        var dir = Path.Combine(context.CacheDir!.AbsolutePath, "updates");
        Directory.CreateDirectory(dir);
        var apkPath = Path.Combine(dir, manifest.Apk);
        try
        {
            // HttpClient.Timeout only covers the response headers of GetStreamAsync; bound the body read too, or a
            // stalled server would hang this loop (and every later check) until the process restarts
            using (var downloadTimeout = new CancellationTokenSource(DownloadTimeout))
            await using (var source = await http.GetStreamAsync(UpdateManifest.CombineUrl(url, manifest.Apk), downloadTimeout.Token))
            await using (var target = File.Create(apkPath))
            {
                await source.CopyToAsync(target, downloadTimeout.Token);
            }

            bool hashOk;
            await using (var stream = File.OpenRead(apkPath))
            {
                hashOk = manifest.VerifySha256(stream);
            }

            if (!hashOk)
            {
                Log.Error(AndroidLog.Tag, $"Update: SHA-256 mismatch for {manifest.Apk}; refusing to install");
                UpdatePrefs.RecordResult(context, "hash-mismatch");
                return;
            }

            Log.Info(AndroidLog.Tag, "Update: SHA-256 verified; waiting for idle");
            UpdatePrefs.RecordResult(context, $"downloaded:{manifest.VersionCode}:waiting-for-idle");
            while (IsPlaying())
            {
                await Task.Delay(IdlePoll);
            }

            Log.Info(AndroidLog.Tag, "Update: idle; installing");
            UpdatePrefs.RecordResult(context, $"installing:{manifest.VersionCode}");
            Install(context, apkPath);
        }
        finally
        {
            // Install() copies the bytes into the session, so the cached file is no longer needed either way
            try { File.Delete(apkPath); } catch (IOException) { }
        }
    }

    private static bool IsPlaying()
    {
        var player = Locator.Current.GetService<IPlayerService>();
        return player is { Current: not null, Paused: false };
    }

    private static long GetInstalledVersionCode(Context context)
    {
#pragma warning disable CA1422
        var info = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!;
        return OperatingSystem.IsAndroidVersionAtLeast(28) ? info.LongVersionCode : info.VersionCode;
#pragma warning restore CA1422
    }

    private static void Install(Context context, string apkPath)
    {
        var installer = context.PackageManager!.PackageInstaller;
        var sessionParams = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
        sessionParams.SetAppPackageName(context.PackageName);
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            sessionParams.SetRequireUserAction((int)PackageInstallUserAction.NotRequired);
        }

        var sessionId = installer.CreateSession(sessionParams);
        using var session = installer.OpenSession(sessionId)!;
        var length = new FileInfo(apkPath).Length;
        using (var input = File.OpenRead(apkPath))
        using (var output = session.OpenWrite("resona", 0, length)!)
        {
            input.CopyTo(output);
            session.Fsync(output);
        }

        var intent = new Intent(context, typeof(UpdateInstallResultReceiver));
        // Mutable is required: the installer adds the status extras to this intent
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            flags |= PendingIntentFlags.Mutable;
        }

        var pending = PendingIntent.GetBroadcast(context, sessionId, intent, flags)!;
        session.Commit(pending.IntentSender!);
        Log.Info(AndroidLog.Tag, $"Update: install session {sessionId} committed");
    }
}
