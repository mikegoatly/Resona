using System.Collections.Concurrent;

using Serilog;

namespace Resona.Services.Bluetooth
{
    public sealed class DevBluetoothService : BluetoothServiceBase
    {
        private readonly Timer scanTimer;
        private int scanCount;
        private static readonly ConcurrentBag<BluetoothDevice> knownDevices = new()
        {
            new BluetoothDevice("My Phone", Guid.NewGuid().ToString(), false),
            new BluetoothDevice("Bluetooth Headphones", Guid.NewGuid().ToString(), false, true),
            new BluetoothDevice("Someone's TV", Guid.NewGuid().ToString(), true)
        };

        public DevBluetoothService()
            : base(Log.ForContext<DevBluetoothService>())
        {
            this.scanTimer = new Timer(this.ScanTimerTicked, null, Timeout.Infinite, Timeout.Infinite);
        }

        public override Task StartScanningAsync(CancellationToken cancellationToken)
        {
            this.scanCount = 0;
            this.OnStartScanning();
            this.scanTimer.Change(1000, 1000);

            return Task.CompletedTask;
        }

        public override Task StopScanningAsync(CancellationToken cancellationToken)
        {
            this.scanTimer.Change(Timeout.Infinite, Timeout.Infinite);

            this.OnStopScanning();

            return Task.CompletedTask;
        }

        public override Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyList<BluetoothDevice>)knownDevices.ToList());
        }

        public override async Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken)
        {
            device.Status = DeviceStatus.Connecting;
            await Task.Delay(2000, cancellationToken);
            device.Status = DeviceStatus.Connected;
            this.OnDeviceConnected(device);
        }

        private void ScanTimerTicked(object? state)
        {
            if (++this.scanCount == 2)
            {
                this.StopScanningAsync(default);
            }

            var address = Guid.NewGuid().ToString();
            var newDevice = new BluetoothDevice(address[4..] + " another random device", address);

            this.OnDeviceDiscovered(newDevice);

            knownDevices.Add(newDevice);
        }

        public override Task ForgetDeviceAsync(BluetoothDevice device, CancellationToken cancellationToken)
        {
            device.Paired = false;
            return Task.CompletedTask;
        }
    }
}
