using System.Reactive.Subjects;

using Serilog;

namespace Resona.Services.Bluetooth
{
    public abstract class BluetoothServiceBase : IBluetoothService, IDisposable
    {
        private readonly Timer scanTimeout;
        private readonly ILogger logger;
        private readonly Subject<BluetoothDevice> bluetoothDeviceDiscovered = new();
        private readonly Subject<BluetoothDevice> bluetoothDeviceConnected = new();
        private readonly Subject<bool> scanningStateChanged = new();

        public BluetoothServiceBase(ILogger logger)
        {
            this.scanTimeout = new Timer(this.ScanTimeoutTicked, null, Timeout.Infinite, Timeout.Infinite);
            this.logger = logger;
        }

        public IObservable<BluetoothDevice> BluetoothDeviceDiscovered => this.bluetoothDeviceDiscovered;
        public IObservable<BluetoothDevice> BluetoothDeviceConnected => this.bluetoothDeviceConnected;
        public IObservable<bool> ScanningStateChanged => this.scanningStateChanged;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.scanTimeout?.Dispose();
            }
        }

        protected void OnStartScanning()
        {
            this.logger.Information("Starting scanning...");

            this.scanningStateChanged.OnNext(true);

            this.scanTimeout.Change(30000, Timeout.Infinite);
        }

        protected void OnStopScanning()
        {
            this.logger.Information("Stopping scanning...");
            this.scanTimeout.Change(Timeout.Infinite, Timeout.Infinite);
            this.scanningStateChanged.OnNext(false);
        }

        protected void OnDeviceDiscovered(BluetoothDevice device)
        {
            this.logger.Information("New device discovered: {Name} [{Mac}]", device.Name, device.Address);
            this.bluetoothDeviceDiscovered.OnNext(device);
        }

        protected void OnDeviceConnected(BluetoothDevice device)
        {
            device.Status = DeviceStatus.Connected;

            this.logger.Information("Device connected: {Name} [{Mac}]", device.Name, device.Address);
            this.bluetoothDeviceConnected.OnNext(device);
        }

        private async void ScanTimeoutTicked(object? state)
        {
            try
            {
                await this.StopScanningAsync(default);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Error stopping scanning");
            }
        }

        public abstract Task StartScanningAsync(CancellationToken cancellationToken);
        public abstract Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken);
        public abstract Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken);
        public abstract Task StopScanningAsync(CancellationToken cancellationToken);
    }
}