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
        private Adapter? adapter;
        private static readonly ILogger logger = Log.ForContext<BluetoothService>();

        public BluetoothService()
            : base(logger)
        {
        }

        public override async Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken)
        {
            using var nameContext = LogContext.PushProperty("Name", device.Name);
            using var macContext = LogContext.PushProperty("Mac", device.Address);

            var adapter = await this.GetAdapterAsync();

            var bluezDevice = await adapter.GetDeviceAsync(device.Address);
            if (bluezDevice == null)
            {
                logger.Information("Device not found");
                return;
            }

            var properties = await bluezDevice.GetAllAsync();
            if (properties.Connected)
            {
                logger.Information("Already connected");
                this.OnDeviceConnected(device);
                return;
            }

            if (!properties.Paired)
            {
                //logger.Information("Removing device");
                //await adapter.RemoveDeviceAsync(bluezDevice.ObjectPath);

                logger.Information("Trusting device");
                await bluezDevice.SetTrustedAsync(true);

                logger.Information("Pairing device");
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

            logger.Information("Connecting device");

            try
            {
                await bluezDevice.ConnectAsync();

                if (await connectedSemaphore.WaitAsync(5000))
                {
                    this.OnDeviceConnected(device);
                }
                else
                {
                    logger.Warning("Timeout connecting to device");

                    // Refetch the properties to see if we have connected
                    properties = await bluezDevice.GetAllAsync();
                    if (properties.Connected)
                    {
                        logger.Information("Device did connect");
                        this.OnDeviceConnected(device);
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
            var adapter = await this.GetAdapterAsync();

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
            if (this.adapter == null)
            {
                this.adapter = (await BlueZManager.GetAdaptersAsync()).FirstOrDefault()
                    ?? throw new InvalidOperationException("No bluetooth adapters found!");

                this.adapter.DeviceFound += this.AdapterDeviceFound;
            }

            return this.adapter;
        }

        private async Task AdapterDeviceFound(Adapter sender, DeviceFoundEventArgs eventArgs)
        {
            this.OnDeviceDiscovered(await GetDeviceInfoAsync(eventArgs.Device));
        }

        public override async Task StartScanningAsync(CancellationToken cancellationToken)
        {
            this.OnStartScanning();

            var adapter = await this.GetAdapterAsync();

            await adapter.StartDiscoveryAsync();
        }

        public override async Task StopScanningAsync(CancellationToken cancellationToken)
        {
            this.OnStopScanning();

            var adapter = await this.GetAdapterAsync();

            await adapter.StopDiscoveryAsync();
        }
    }
}
