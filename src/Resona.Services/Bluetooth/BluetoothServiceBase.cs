using Serilog;

namespace Resona.Services.Bluetooth
{
    public abstract class BluetoothServiceBase : IBluetoothService, IDisposable
    {
        private readonly Timer scanTimeout;
        private readonly ILogger logger;

        public event EventHandler<BluetoothDevice>? BluetoothDeviceDiscovered;
        public event EventHandler<BluetoothDevice>? BluetoothDeviceConnected;
        public event EventHandler? ScanningStopped;

        public BluetoothServiceBase(ILogger logger)
        {
            this.scanTimeout = new Timer(this.ScanTimeoutTicked, null, Timeout.Infinite, Timeout.Infinite);
            this.logger = logger;
        }

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

            this.scanTimeout.Change(30000, Timeout.Infinite);
        }

        protected void OnStopScanning()
        {
            this.logger.Information("Stopping scanning...");
            this.scanTimeout.Change(Timeout.Infinite, Timeout.Infinite);
            ScanningStopped?.Invoke(this, EventArgs.Empty);
        }

        protected void OnDeviceDiscovered(BluetoothDevice device)
        {
            this.logger.Information("New device discovered: {Name} [{Mac}]", device.Name, device.Address);
            BluetoothDeviceDiscovered?.Invoke(this, device);
        }

        protected void OnDeviceConnected(BluetoothDevice device)
        {
            this.logger.Information("Device connected: {Name} [{Mac}]", device.Name, device.Address);
            BluetoothDeviceConnected?.Invoke(this, device);
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