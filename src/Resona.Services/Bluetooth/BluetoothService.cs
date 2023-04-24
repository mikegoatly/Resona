using ProrepubliQ.DotNetBlueZ;

using Serilog;
using Serilog.Context;

namespace Resona.Services.Bluetooth
{
    public interface IBluetoothService
    {
        IObservable<BluetoothDevice> BluetoothDeviceDiscovered { get; }
        IObservable<BluetoothDevice> BluetoothDeviceConnected { get; }
        IObservable<bool> ScanningStateChanged { get; }

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

            try
            {
                var adapter = await this.GetAdapterAsync();

                var bluezDevice = await adapter.GetDeviceAsync(device.Address);
                if (bluezDevice == null)
                {
                    logger.Information("Device not found");
                    return;
                }

                if (await bluezDevice.GetConnectedAsync())
                {
                    logger.Information("Already connected");
                    device.Status = DeviceStatus.Connected;
                    this.OnDeviceConnected(device);
                    return;
                }

                device.Status = DeviceStatus.Connecting;

                if (await bluezDevice.GetPairedAsync())
                {
                    logger.Information("Already paired");
                }
                else
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
                    logger.Debug("Received connected event");
                    connectedSemaphore.Release();
                    return Task.CompletedTask;
                }

                bluezDevice.Connected += BluezDeviceConnected;

                logger.Information("Connecting device");

                try
                {
                    await bluezDevice.ConnectAsync();

                    if (await connectedSemaphore.WaitAsync(5000, cancellationToken))
                    {
                        this.OnDeviceConnected(device);
                    }
                    else
                    {
                        logger.Warning("Timeout connecting to device");

                        // Re-fetch the properties to see if we have connected
                        if (await bluezDevice.GetConnectedAsync())
                        {
                            logger.Information("Device connected");
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
            catch (Exception ex)
            {
                logger.Error(ex, "Error connecting to device");
                device.Status = DeviceStatus.NotConnected;
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
            var name = await device.GetNameAsync();
            var alias = await device.GetAliasAsync();
            var address = await device.GetAddressAsync();
            var connected = await device.GetConnectedAsync();

            return new BluetoothDevice(alias ?? name ?? address, address, connected);
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
