using PiJuiceSharp;

namespace Resona.Services.Battery
{
    public interface IBatteryStateProvider
    {
        IObservable<int> BatteryLevel { get; }
        IObservable<StatusInfo> ChargingStatus { get; }
    }
}
