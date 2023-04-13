using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

namespace Resona.Services.Bluetooth
{
    public abstract class BluetoothServiceBase : IBluetoothService, IDisposable
    {
        private readonly Timer _scanTimeout;
        private readonly ILogger _logger;

        public event EventHandler<BluetoothDevice>? BluetoothDeviceDiscovered;
        public event EventHandler<BluetoothDevice>? BluetoothDeviceConnected;
        public event EventHandler? ScanningStopped;

        public BluetoothServiceBase(ILogger logger)
        {
            _scanTimeout = new Timer(ScanTimeoutTicked, null, Timeout.Infinite, Timeout.Infinite);
            _logger = logger;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                _scanTimeout?.Dispose();
            }
        }

        protected void OnStartScanning()
        {
            _logger.Information("Starting scanning...");

            _scanTimeout.Change(30000, Timeout.Infinite);
        }

        protected void OnStopScanning()
        {
            _logger.Information("Stopping scanning...");
            _scanTimeout.Change(Timeout.Infinite, Timeout.Infinite);
            ScanningStopped?.Invoke(this, EventArgs.Empty);
        }

        protected void OnDeviceDiscovered(BluetoothDevice device)
        {
            _logger.Information("New device discovered: {Name} [{Mac}]", device.Name, device.Address);
            BluetoothDeviceDiscovered?.Invoke(this, device);
        }

        protected void OnDeviceConnected(BluetoothDevice device)
        {
            _logger.Information("Device connected: {Name} [{Mac}]", device.Name, device.Address);
            BluetoothDeviceConnected?.Invoke(this, device);
        }

        private async void ScanTimeoutTicked(object? state)
        {
            try
            {
                await StopScanningAsync(default);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error stopping scanning");
            }
        }

        public abstract Task StartScanningAsync(CancellationToken cancellationToken);
        public abstract Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken);
        public abstract Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken);
        public abstract Task StopScanningAsync(CancellationToken cancellationToken);
    }
}