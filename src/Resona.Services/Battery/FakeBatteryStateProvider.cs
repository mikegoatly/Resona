using System.Reactive.Linq;
using System.Reactive.Subjects;

using PiJuiceSharp;

namespace Resona.Services.Battery
{
    public class FakeBatteryStateProvider : IBatteryStateProvider
    {
        private readonly ReplaySubject<int> batteryLevel = new();

        public FakeBatteryStateProvider()
        {
            this.batteryLevel.OnNext(80);
        }

        public IObservable<int> BatteryLevel => this.batteryLevel;

        public IObservable<StatusInfo> ChargingStatus { get; } = new[] { new StatusInfo() }.ToObservable();
    }
}
