using Android.App;
using Android.App.Admin;
using Android.Content;

namespace Resona.Android;

/// <summary>
/// The device admin component. Provision with
/// <c>adb shell dpm set-device-owner com.resona/com.resona.ResonaDeviceAdminReceiver</c>.
/// </summary>
[BroadcastReceiver(
    Name = ResonaDeviceAdminReceiver.ClassName,
    Permission = "android.permission.BIND_DEVICE_ADMIN",
    Exported = true)]
[MetaData("android.app.device_admin", Resource = "@xml/device_admin")]
[IntentFilter(new[] { "android.app.action.DEVICE_ADMIN_ENABLED" })]
public class ResonaDeviceAdminReceiver : DeviceAdminReceiver
{
    public const string ClassName = "com.resona.ResonaDeviceAdminReceiver";

    public static ComponentName GetComponent(Context context) => new(context.PackageName!, ClassName);
}
