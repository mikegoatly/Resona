using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

namespace Resona.Services.Bluetooth
{
    public sealed class DevBluetoothService : BluetoothServiceBase
    {
        private readonly Timer _scanTimer;
        private static readonly ConcurrentBag<BluetoothDevice> _knownDevices = new()
        {
            new BluetoothDevice("Initial device", Guid.NewGuid().ToString(), true)
        };

        public DevBluetoothService()
            : base(Log.ForContext<DevBluetoothService>())
        {
            _scanTimer = new Timer(ScanTimerTicked, null, Timeout.Infinite, Timeout.Infinite);
        }

        public override Task StartScanningAsync(CancellationToken cancellationToken)
        {
            OnStartScanning();
            _scanTimer.Change(1000, 1000);
            return Task.CompletedTask;
        }

        public override Task StopScanningAsync(CancellationToken cancellationToken)
        {
            OnStopScanning();

            _scanTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        public override Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyList<BluetoothDevice>)_knownDevices.ToList());
        }

        public override async Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken)
        {
            await Task.Delay(2000, cancellationToken);
            device.Connected = true;
            OnDeviceConnected(device);
        }

        private void ScanTimerTicked(object? state)
        {
            var address = Guid.NewGuid().ToString();
            var newDevice = new BluetoothDevice("Test " + address, address);

            OnDeviceDiscovered(newDevice);

            _knownDevices.Add(newDevice);
        }
    }
}
