using Android.App;
using Android.Content;
using Android.Util;

using Resona.Android.Updates;

namespace Resona.Android;

/// <summary>
/// Parent control over adb. Protected by the DUMP permission, which only the adb shell (and the system) holds, so
/// the child cannot invoke it from the device itself.
/// </summary>
[BroadcastReceiver(Name = KioskCommandReceiver.ClassName, Permission = "android.permission.DUMP", Exported = true)]
public class KioskCommandReceiver : BroadcastReceiver
{
    public const string ClassName = "com.resona.KioskCommandReceiver";
    public const string ActionDisable = "com.resona.action.KIOSK_DISABLE";
    public const string ActionEnable = "com.resona.action.KIOSK_ENABLE";
    public const string ActionRelease = "com.resona.action.RELEASE_OWNER";
    public const string ActionSetUpdateSource = "com.resona.action.SET_UPDATE_SOURCE";
    public const string ActionCheckUpdate = "com.resona.action.CHECK_UPDATE";
    public const string ActionUpdateStatus = "com.resona.action.UPDATE_STATUS";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        var action = intent.Action;
        string result;
        int code;
        try
        {
            if (!KioskPolicy.IsDeviceOwner(context))
            {
                (code, result) = (2, "not-device-owner");
            }
            else
            {
                switch (action)
                {
                    case ActionDisable:
                        KioskPolicy.Disable(context);
                        (code, result) = (1, "kiosk-disabled");
                        break;
                    case ActionEnable:
                        KioskPolicy.Apply(context);
                        KioskPolicy.LaunchMainActivity(context);
                        (code, result) = (1, "kiosk-enabled");
                        break;
                    case ActionRelease:
                        KioskPolicy.ReleaseOwner(context);
                        (code, result) = (1, "owner-released");
                        break;
                    case ActionSetUpdateSource:
                        var url = intent.GetStringExtra("url");
                        UpdatePrefs.SetUrl(context, url);
                        (code, result) = (1, string.IsNullOrWhiteSpace(url) ? "update-source-cleared" : "update-source-set");
                        break;
                    case ActionCheckUpdate:
                        UpdateService.CheckNow();
                        (code, result) = (1, "update-check-requested");
                        break;
                    case ActionUpdateStatus:
                        var (time, last) = UpdatePrefs.GetLast(context);
                        (code, result) = (1, $"url={UpdatePrefs.GetUrl(context) ?? "-"};lastCheck={time ?? "-"};lastResult={last ?? "-"};hidden={KioskPolicy.HiddenCount(context)}");
                        break;
                    default:
                        (code, result) = (3, "unknown-action");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(AndroidLog.Tag, $"Kiosk command {action} failed: {ex}");
            (code, result) = (4, "error:" + ex.Message);
        }

        Log.Info(AndroidLog.Tag, $"Kiosk command {action}: {result}");
        if (this.IsOrderedBroadcast)
        {
            this.ResultCode = (Result)code;
            this.ResultData = result;
        }
    }
}
