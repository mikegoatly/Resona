using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ProrepubliQ.DotNetBlueZ;

using Serilog;
using Serilog.Context;

namespace Resona.Services.Bluetooth
{
    public interface IBluetoothService
    {
        event EventHandler<BluetoothDevice>? BluetoothDeviceDiscovered;
        event EventHandler<BluetoothDevice>? BluetoothDeviceConnected;
        event EventHandler? ScanningStopped;

        Task StartScanningAsync(CancellationToken cancellationToken);
        Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken);
        Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken);
        Task StopScanningAsync(CancellationToken cancellationToken);
    }

    public class BluetoothService : BluetoothServiceBase
    {
        private Adapter? _adapter;
        private static readonly ILogger _logger = Log.ForContext<BluetoothService>();

        public BluetoothService()
            : base(_logger)
        {
        }

        public override async Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken)
        {
            using var nameContext = LogContext.PushProperty("Name", device.Name);
            using var macContext = LogContext.PushProperty("Mac", device.Address);

            var adapter = await GetAdapterAsync();

            var bluezDevice = await adapter.GetDeviceAsync(device.Address);
            if (bluezDevice == null)
            {
                _logger.Information("Device not found");
                return;
            }

            var properties = await bluezDevice.GetAllAsync();
            if (properties.Connected)
            {
                _logger.Information("Already connected");
                OnDeviceConnected(device);
                return;
            }

            if (!properties.Paired)
            {
                //logger.Information("Removing device");
                //await adapter.RemoveDeviceAsync(bluezDevice.ObjectPath);

                _logger.Information("Trusting device");
                await bluezDevice.SetTrustedAsync(true);

                _logger.Information("Pairing device");
                await bluezDevice.PairAsync();
            }

            // We use a semaphore to control the code flow so that we 
            // wait until the device is fully connected before continuing
            var connectedSemaphore = new SemaphoreSlim(0);
            Task BluezDeviceConnected(Device sender, BlueZEventArgs eventArgs)
            {
                connectedSemaphore.Release();
                return Task.CompletedTask;
            }

            bluezDevice.Connected += BluezDeviceConnected;

            _logger.Information("Connecting device");

            try
            {
                await bluezDevice.ConnectAsync();

                if (await connectedSemaphore.WaitAsync(5000))
                {
                    OnDeviceConnected(device);
                }
                else
                {
                    _logger.Warning("Timeout connecting to device");

                    // Refetch the properties to see if we have connected
                    properties = await bluezDevice.GetAllAsync();
                    if (properties.Connected)
                    {
                        _logger.Information("Device did connect");
                        OnDeviceConnected(device);
                        return;
                    }
                }
            }
            finally
            {
                bluezDevice.Connected -= BluezDeviceConnected;
            }
        }

        public override async Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken)
        {
            var adapter = await GetAdapterAsync();

            var devices = new List<BluetoothDevice>();

            foreach (var device in await adapter.GetDevicesAsync())
            {
                devices.Add(await GetDeviceInfoAsync(device));
            }

            return devices.OrderBy(d => d.Name).ToList();
        }

        private static async Task<BluetoothDevice> GetDeviceInfoAsync(Device device)
        {
            //Console.WriteLine($"Name: {props.Name} Alias: {props.Alias} Paired: {props.Paired} Trusted: {props.Trusted} Connected: {props.Connected} Address: {props.Address} RSSI: {props.RSSI}");
            //if (props.ManufacturerData != null)
            //{
            //    foreach (var prop in props.ManufacturerData)
            //    {
            //        Console.WriteLine($"    {prop.Key}:{prop.Value}");
            //    }
            //}

            var props = await device.GetAllAsync();
            return new BluetoothDevice(props.Alias ?? props.Address, props.Address, props.Connected);
        }

        private async Task<Adapter> GetAdapterAsync()
        {
            if (_adapter == null)
            {
                _adapter = (await BlueZManager.GetAdaptersAsync()).FirstOrDefault()
                    ?? throw new InvalidOperationException("No bluetooth adapters found!");

                _adapter.DeviceFound += AdapterDeviceFound;
            }

            return _adapter;
        }

        private async Task AdapterDeviceFound(Adapter sender, DeviceFoundEventArgs eventArgs)
        {
            OnDeviceDiscovered(await GetDeviceInfoAsync(eventArgs.Device));
        }

        public override async Task StartScanningAsync(CancellationToken cancellationToken)
        {
            OnStartScanning();

            var adapter = await GetAdapterAsync();

            await adapter.StartDiscoveryAsync();
        }

        public override async Task StopScanningAsync(CancellationToken cancellationToken)
        {
            OnStopScanning();

            var adapter = await GetAdapterAsync();

            await adapter.StopDiscoveryAsync();
        }
    }
}
