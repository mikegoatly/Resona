using System;
using System.Reactive.Linq;

using PiJuiceSharp;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Battery;

using Serilog;

namespace Resona.UI.ViewModels
{
    public class BatteryViewModel : ReactiveObject
    {
        private static readonly ILogger logger = Log.ForContext<BatteryViewModel>();

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public BatteryViewModel()
            : this(new FakeBatteryStateProvider())
        {
        }
#endif

        public BatteryViewModel(IBatteryStateProvider batteryState)
        {
            this.BatteryLevel = batteryState.BatteryLevel;

            batteryState.ChargingStatus.Subscribe(status =>
            {
                this.HasBattery = status.BatteryStatus != BatteryStatus.NotPresent;
                this.IsCharging = status.BatteryStatus is BatteryStatus.ChargingFrom5vIo or BatteryStatus.ChargingFromIn;
            });
        }

        public IObservable<int> BatteryLevel { get; }

        [Reactive]
        public bool HasBattery { get; private set; }

        [Reactive]
        public bool IsCharging { get; private set; }
    }
}
