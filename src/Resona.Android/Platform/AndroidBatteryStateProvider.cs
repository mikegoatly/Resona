using System.Reactive.Linq;
using System.Reactive.Subjects;

using Android.Content;
using Android.OS;
using Android.Util;

using PiJuiceSharp;
using PiBatteryStatus = PiJuiceSharp.BatteryStatus;

using Resona.Services.Battery;

namespace Resona.Android.Platform;

/// <summary>
/// Reports the device battery from the sticky <c>ACTION_BATTERY_CHANGED</c> broadcast, so updates are pushed as the
/// level or charging state changes. Android has no PiJuice, so the charging state is mapped onto the same
/// <see cref="StatusInfo"/> semantics the UI uses: <c>Normal</c> (on battery) or <c>ChargingFromIn</c> (plugged in).
/// </summary>
internal sealed class AndroidBatteryStateProvider : IBatteryStateProvider
{

    private readonly BehaviorSubject<int> batteryLevel = new(0);
    private readonly BehaviorSubject<StatusInfo> chargingStatus = new(new StatusInfo());

    public AndroidBatteryStateProvider(Context context)
    {
        // The sticky broadcast delivers the current state immediately on registration
        var receiver = new BatteryReceiver(this);
        var current = context.RegisterReceiver(receiver, new IntentFilter(Intent.ActionBatteryChanged));
        if (current != null)
        {
            this.Update(current);
        }
    }

    public IObservable<int> BatteryLevel => this.batteryLevel.DistinctUntilChanged();

    public IObservable<StatusInfo> ChargingStatus => this.chargingStatus.DistinctUntilChanged();

    private void Update(Intent intent)
    {
        var level = intent.GetIntExtra(BatteryManager.ExtraLevel, -1);
        var scale = intent.GetIntExtra(BatteryManager.ExtraScale, 100);
        if (level >= 0 && scale > 0)
        {
            var percent = Math.Clamp(level * 100 / scale, 0, 100);
            if (percent != this.batteryLevel.Value)
            {
                Log.Info(AndroidLog.Tag, $"Battery level {percent}%");
                this.batteryLevel.OnNext(percent);
            }
        }

        var present = intent.GetBooleanExtra(BatteryManager.ExtraPresent, true);
        var plugged = intent.GetIntExtra(BatteryManager.ExtraPlugged, 0) != 0;
        PiBatteryStatus batteryStatus = !present
            ? PiBatteryStatus.NotPresent
            : plugged ? PiBatteryStatus.ChargingFromIn : PiBatteryStatus.Normal;
        if (batteryStatus != this.chargingStatus.Value.BatteryStatus)
        {
            Log.Info(AndroidLog.Tag, $"Battery status {batteryStatus}");
            this.chargingStatus.OnNext(new StatusInfo { BatteryStatus = batteryStatus });
        }
    }

    private sealed class BatteryReceiver(AndroidBatteryStateProvider owner) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent != null)
            {
                owner.Update(intent);
            }
        }
    }
}
