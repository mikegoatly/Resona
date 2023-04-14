using System.Collections.Concurrent;

using Serilog;

namespace Resona.Services.Bluetooth
{
    public sealed class DevBluetoothService : BluetoothServiceBase
    {
        private readonly Timer scanTimer;
        private static readonly ConcurrentBag<BluetoothDevice> knownDevices = new()
        {
            new BluetoothDevice("Initial device", Guid.NewGuid().ToString(), true)
        };

        public DevBluetoothService()
            : base(Log.ForContext<DevBluetoothService>())
        {
            this.scanTimer = new Timer(this.ScanTimerTicked, null, Timeout.Infinite, Timeout.Infinite);
        }

        public override Task StartScanningAsync(CancellationToken cancellationToken)
        {
            this.OnStartScanning();
            this.scanTimer.Change(1000, 1000);
            return Task.CompletedTask;
        }

        public override Task StopScanningAsync(CancellationToken cancellationToken)
        {
            this.OnStopScanning();

            this.scanTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        public override Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyList<BluetoothDevice>)knownDevices.ToList());
        }

        public override async Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken)
        {
            await Task.Delay(2000, cancellationToken);
            device.Connected = true;
            this.OnDeviceConnected(device);
        }

        private void ScanTimerTicked(object? state)
        {
            var address = Guid.NewGuid().ToString();
            var newDevice = new BluetoothDevice("Test " + address, address);

            this.OnDeviceDiscovered(newDevice);

            knownDevices.Add(newDevice);
        }
    }
}
