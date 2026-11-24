using Android.App;
using Android.App.Admin;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views.InputMethods;

namespace Resona.Android;

/// <summary>
/// All device-owner/lock-task kiosk logic. Everything here is inert unless the app is the device owner, so
/// normal (non device-owner) installs behave like an ordinary app.
/// </summary>
internal static class KioskPolicy
{
    public const string HomeActivityClass = "com.resona.HomeActivity";
    public const string MainActivityClass = "com.resona.MainActivity";

    private const string PrefsName = "resona_kiosk";
    private const string EnabledKey = "enabled";

    private static readonly string[] Restrictions =
    {
        UserManager.DisallowFactoryReset,
        UserManager.DisallowSafeBoot,
        UserManager.DisallowAddUser,
        UserManager.DisallowInstallUnknownSources,
        UserManager.DisallowModifyAccounts,
        // Blocks other apps' overlay/system-alert windows. Resona does not use overlays (Avalonia renders in its activity window).
        UserManager.DisallowCreateWindows,
    };

    private const string HiddenKey = "hidden_packages";

    /// <summary>Packages that must never be hidden. Own package, current IME(s) and alternative HOME launchers are added dynamically.</summary>
    private static readonly HashSet<string> NeverHide = new(StringComparer.Ordinal)
    {
        "com.android.settings",                    // Settings: parent needs it and it is reachable only via adb in kiosk
        "com.miui.securitycenter",                 // MIUI security centre: permission/autostart/battery handling, hiding it can break MIUI
        "com.android.systemui",                    // System UI: status bar, lock task machinery, dialogs
        "com.android.packageinstaller",            // Package installer: needed for updates
        "com.google.android.packageinstaller",     // Package installer (Google builds)
        "com.android.permissioncontroller",        // Permission controller: runtime permission dialogs
        "com.google.android.permissioncontroller", // Permission controller (Google builds)
        "com.miui.packageinstaller",               // MIUI package installer
    };

    /// <summary>Packages hidden even if they have no launcher activity: search/assistant/voice apps that can pop floating windows.</summary>
    private static readonly string[] AlwaysHide =
    {
        "com.google.android.googlequicksearchbox", // Google app / search floating window (the observed escape)
        "com.google.android.apps.googleassistant", // Google Assistant
        "com.miui.voiceassist",                    // MIUI Xiao AI voice assistant
    };

    public static bool IsEnabled(Context context)
        => Prefs(context).GetBoolean(EnabledKey, true);

    public static bool IsDeviceOwner(Context context)
        => GetManager(context).IsDeviceOwnerApp(context.PackageName);

    /// <summary>True when kiosk should be actively enforced: device owner and not disabled by the parent.</summary>
    public static bool IsActive(Context context) => IsDeviceOwner(context) && IsEnabled(context);

    /// <summary>Applies the policy (when active). Idempotent; safe to call on every start.</summary>
    public static void ApplyIfActive(Context context)
    {
        if (!IsDeviceOwner(context))
        {
            return;
        }

        if (!IsEnabled(context))
        {
            Log.Info(AndroidLog.Tag, "Kiosk: device owner but kiosk is disabled; not applying");
            return;
        }

        Apply(context);
    }

    public static void Apply(Context context)
    {
        var dpm = GetManager(context);
        var admin = ResonaDeviceAdminReceiver.GetComponent(context);
        var package = context.PackageName!;

        SetEnabled(context, true);

        dpm.SetLockTaskPackages(admin, new[] { package });
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            dpm.SetLockTaskFeatures(admin, LockTaskFeatures.None);
        }

        // HOME launcher
        context.PackageManager!.SetComponentEnabledSetting(
            new ComponentName(package, HomeActivityClass), ComponentEnabledState.Enabled, ComponentEnableOption.DontKillApp);
        dpm.ClearPackagePersistentPreferredActivities(admin, package);
        dpm.AddPersistentPreferredActivity(admin, CreateHomeFilter(), new ComponentName(package, HomeActivityClass));

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            dpm.SetKeyguardDisabled(admin, true);
            dpm.SetStatusBarDisabled(admin, true);
        }

        foreach (var restriction in Restrictions)
        {
            dpm.AddUserRestriction(admin, restriction);
        }

        HideOtherApps(context, dpm, admin);

        Log.Info(AndroidLog.Tag, "Kiosk: policy applied");
    }

    /// <summary>Number of apps Resona currently has hidden.</summary>
    public static int HiddenCount(Context context) => GetHidden(context).Count;

    private static HashSet<string> GetHidden(Context context)
    {
        var set = Prefs(context).GetStringSet(HiddenKey, null);
        return set is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(set, StringComparer.Ordinal);
    }

    private static void SaveHidden(Context context, HashSet<string> hidden)
        => Prefs(context).Edit()!.PutStringSet(HiddenKey, hidden)!.Commit();

    private static void HideOtherApps(Context context, DevicePolicyManager dpm, ComponentName admin)
    {
        var pm = context.PackageManager!;
        var keep = new HashSet<string>(NeverHide, StringComparer.Ordinal) { context.PackageName! };

        // Current default and enabled IMEs
        var defaultIme = Settings.Secure.GetString(context.ContentResolver, "default_input_method");
        if (defaultIme is not null)
        {
            keep.Add(ComponentName.UnflattenFromString(defaultIme)?.PackageName ?? defaultIme);
        }

        if (context.GetSystemService(Context.InputMethodService) is InputMethodManager imm && imm.EnabledInputMethodList is { } imes)
        {
            foreach (var ime in imes)
            {
                keep.Add(ime.PackageName ?? string.Empty);
            }
        }

        // Any HOME launcher other than ours, so KioskOff can return to one
        var homeIntent = new Intent(Intent.ActionMain).AddCategory(Intent.CategoryHome);
        foreach (var info in pm.QueryIntentActivities(homeIntent, PackageInfoFlags.MatchAll) ?? new List<ResolveInfo>())
        {
            if (info.ActivityInfo?.PackageName is { } homePackage)
            {
                keep.Add(homePackage);
            }
        }

        // MatchUninstalledPackages (0x2000) so already-hidden apps are still enumerated
        var launcherIntent = new Intent(Intent.ActionMain).AddCategory(Intent.CategoryLauncher);
        var candidates = new HashSet<string>(AlwaysHide, StringComparer.Ordinal);
        foreach (var info in pm.QueryIntentActivities(launcherIntent, PackageInfoFlags.MatchAll | (PackageInfoFlags)0x2000) ?? new List<ResolveInfo>())
        {
            if (info.ActivityInfo?.ApplicationInfo is { } app)
            {
                var persistent = (app.Flags & ApplicationInfoFlags.Persistent) != 0;
                if (!persistent)
                {
                    candidates.Add(app.PackageName!);
                }
            }
        }

        var hidden = GetHidden(context);
        var newlyHidden = 0;
        foreach (var package in candidates)
        {
            if (keep.Contains(package))
            {
                continue;
            }

            try
            {
                // Still hidden: either ours from a previous run, or hidden independently of us (left alone, so
                // Disable never unhides it). Always checked, because an OTA/system-app update or `pm unhide` can
                // make one of ours visible again and it must be re-hidden.
                if (dpm.IsApplicationHidden(admin, package))
                {
                    continue;
                }

                // Returns false if the package is not installed (e.g. an AlwaysHide entry)
                if (dpm.SetApplicationHidden(admin, package, true))
                {
                    if (hidden.Add(package))
                    {
                        newlyHidden++;
                    }
                    else
                    {
                        Log.Info(AndroidLog.Tag, $"Kiosk: re-hid {package} (it had become visible)");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(AndroidLog.Tag, $"Kiosk: could not hide {package}: {ex.Message}");
            }
        }

        if (newlyHidden > 0)
        {
            SaveHidden(context, hidden);
        }


        Log.Info(AndroidLog.Tag, $"Kiosk: hid {hidden.Count} apps ({newlyHidden} new)");
    }

    private static void UnhideApps(Context context, DevicePolicyManager dpm, ComponentName admin)
    {
        var hidden = GetHidden(context);
        var count = 0;
        foreach (var package in hidden)
        {
            try
            {
                if (dpm.SetApplicationHidden(admin, package, false))
                {
                    count++;
                }
            }
            catch (Exception ex)
            {
                Log.Warn(AndroidLog.Tag, $"Kiosk: could not unhide {package}: {ex.Message}");
            }
        }

        SaveHidden(context, new HashSet<string>(StringComparer.Ordinal));
        Log.Info(AndroidLog.Tag, $"Kiosk: unhid {count} apps");
    }

    public static void Disable(Context context)
    {
        var dpm = GetManager(context);
        var admin = ResonaDeviceAdminReceiver.GetComponent(context);
        var package = context.PackageName!;

        SetEnabled(context, false);

        // Clearing the lock task packages ends lock task mode for the running task
        dpm.SetLockTaskPackages(admin, Array.Empty<string>());
        dpm.ClearPackagePersistentPreferredActivities(admin, package);
        context.PackageManager!.SetComponentEnabledSetting(
            new ComponentName(package, HomeActivityClass), ComponentEnabledState.Disabled, ComponentEnableOption.DontKillApp);

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            dpm.SetKeyguardDisabled(admin, false);
            dpm.SetStatusBarDisabled(admin, false);
        }

        foreach (var restriction in Restrictions)
        {
            dpm.ClearUserRestriction(admin, restriction);
        }

        UnhideApps(context, dpm, admin);

        Log.Info(AndroidLog.Tag, "Kiosk: policy cleared");
    }

    public static void ReleaseOwner(Context context)
    {
        Disable(context);

        // Reset the flag so a later re-provision starts with kiosk enabled
        SetEnabled(context, true);
        // Deprecated since API 26 but still the only way for a device owner to give up ownership without a factory reset
#pragma warning disable CA1422
        GetManager(context).ClearDeviceOwnerApp(context.PackageName);
#pragma warning restore CA1422
        Log.Info(AndroidLog.Tag, "Kiosk: device owner released");
    }

    private static IntentFilter CreateHomeFilter()
    {
        var filter = new IntentFilter(Intent.ActionMain);
        filter.AddCategory(Intent.CategoryHome);
        filter.AddCategory(Intent.CategoryDefault);
        return filter;
    }

    private static void SetEnabled(Context context, bool enabled)
    {
        if (IsEnabled(context) == enabled)
        {
            return;
        }

        // Commit (not Apply) on purpose: callers rely on the value being on disk before they continue
        Prefs(context).Edit()!.PutBoolean(EnabledKey, enabled)!.Commit();
    }

    private static ISharedPreferences Prefs(Context context)
        => context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;

    internal static DevicePolicyManager GetManager(Context context)
        => (DevicePolicyManager)context.GetSystemService(Context.DevicePolicyService)!;

    /// <summary>Brings <see cref="MainActivity"/> to the front (the single kiosk UI).</summary>
    internal static void LaunchMainActivity(Context context)
    {
        var intent = new Intent(context, typeof(MainActivity)).AddFlags(ActivityFlags.NewTask | ActivityFlags.ReorderToFront);
        context.StartActivity(intent);
    }
}
