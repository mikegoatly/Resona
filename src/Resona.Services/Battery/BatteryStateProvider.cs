using System.Reactive.Linq;

using PiJuiceSharp;

using Serilog;

namespace Resona.Services.Battery
{
    public class BatteryStateProvider : IBatteryStateProvider
    {
        private static readonly ILogger logger = Log.ForContext<BatteryStateProvider>();
        private readonly PiJuiceStatus piJuiceStatus;

        public BatteryStateProvider(
            PiJuiceStatus piJuiceStatus)
        {
            this.piJuiceStatus = piJuiceStatus;

            logger.Information("Starting battery state provider");

            this.BatteryLevel = Observable
                .Interval(TimeSpan.FromSeconds(5))
                .Select(_ => this.piJuiceStatus.GetChargeLevel())
                .DistinctUntilChanged()
                .Do(i => logger.Information("Battery level updated to {Level}", i));

            this.ChargingStatus = Observable
                .Interval(TimeSpan.FromSeconds(5))
                .Select(_ => this.piJuiceStatus.GetStatus())
                .DistinctUntilChanged()
                .Do(i => logger.Information("Battery status updated to {Status}", i.BatteryStatus));
        }

        /// <summary>
        /// An observable that every 5 seconds polls the PiJuice to get the battery level and 
        /// return it when it changes.
        /// </summary>
        public IObservable<int> BatteryLevel { get; }

        public IObservable<StatusInfo> ChargingStatus { get; }
    }
}
