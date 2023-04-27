using System.Collections.Concurrent;
using System.Reactive;

using ProrepubliQ.DotNetBlueZ;

using Serilog;
using Serilog.Context;

namespace Resona.Services.Bluetooth
{
    public interface IBluetoothService
    {
        IObservable<BluetoothDevice> BluetoothDeviceDiscovered { get; }
        IObservable<BluetoothDevice> BluetoothDeviceConnected { get; }
        IObservable<Unit> BluetoothDeviceDisconnected { get; }
        IObservable<bool> ScanningStateChanged { get; }

        Task StartScanningAsync(CancellationToken cancellationToken);
        Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken);
        Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken);
        Task StopScanningAsync(CancellationToken cancellationToken);

        Task ForgetDeviceAsync(BluetoothDevice device, CancellationToken cancellationToken);
    }

    public class LinuxBluetoothService : BluetoothServiceBase
    {
        private readonly ConcurrentDictionary<string, (Device bluezDevice, BluetoothDevice device)> knownDevices = new();
        private Adapter? adapter;
        private static readonly ILogger logger = Log.ForContext<LinuxBluetoothService>();

        public LinuxBluetoothService()
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
                    logger.Warning("Device not found");
                    return;
                }

                if (await bluezDevice.GetConnectedAsync())
                {
                    logger.Information("Already connected");
                    this.OnDeviceConnected(device);
                    return;
                }

                device.Status = DeviceStatus.Connecting;

                await this.EnsureDevicePaired(bluezDevice);

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
            }
            finally
            {
                await this.RefreshDeviceStateAsync(device);
            }
        }

        private async Task RefreshDeviceStateAsync(Device device)
        {
            await this.GetDeviceInfoAsync(device);
        }

        private async Task RefreshDeviceStateAsync(BluetoothDevice device)
        {
            var bluezDevice = await this.adapter.GetDeviceAsync(device.Address);
            if (bluezDevice == null)
            {
                logger.Warning("Device not found when refreshing state");
                device.Status = DeviceStatus.NotConnected;
            }
            else
            {
                await this.RefreshDeviceStateAsync(bluezDevice);
            }
        }

        private async Task EnsureDevicePaired(Device bluezDevice)
        {
            if (await bluezDevice.GetPairedAsync())
            {
                logger.Information("Already paired");
            }
            else
            {
                logger.Information("Trusting device");
                await bluezDevice.SetTrustedAsync(true);

                logger.Information("Pairing device");
                await bluezDevice.PairAsync();
            }
        }

        public override async Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken)
        {
            if (this.knownDevices.Count == 0)
            {
                // Calling this will make sure that the adapter is hooked up and has populated the initial
                // list with any known devices
                await (await this.GetAdapterAsync()).GetDevicesAsync();
            }

            return this.knownDevices.Select(x => x.Value.device)
                .OrderBy(x => x.Name)
                .ToList();
        }

        private async Task<BluetoothDevice> GetDeviceInfoAsync(Device bluezDevice)
        {
            var name = await bluezDevice.GetNameAsync();
            var alias = await bluezDevice.GetAliasAsync();
            var address = await bluezDevice.GetAddressAsync();
            var connected = await bluezDevice.GetConnectedAsync();
            var trusted = await bluezDevice.GetTrustedAsync();
            var paired = await bluezDevice.GetPairedAsync();

            BluetoothDevice? device = null;
            if (this.knownDevices.TryGetValue(address, out var cachedInfo))
            {
                // We're already tracking this device, so we'll return a modified version of the existing one
                device = cachedInfo.device;
                device.Name = alias ?? name ?? address;
                device.Status = connected ? DeviceStatus.Connected : DeviceStatus.NotConnected;
                device.Paired = paired;
                device.Trusted = trusted;

                if (cachedInfo.bluezDevice != bluezDevice)
                {
                    logger.Verbose("BluezDevice instance differs");

                    // Detach from the original device
                    cachedInfo.bluezDevice.Disconnected -= this.DeviceDisconnected;

                    // Attach to the new device presented to us
                    bluezDevice.Disconnected += this.DeviceDisconnected;
                }
                else
                {
                    logger.Verbose("BluezDevice instance matches");
                }

                // Update the dictionary with the new bluez device
                this.knownDevices[address] = (bluezDevice, device);
            }
            else
            {
                // This is a new device, so we'll create a new instance
                logger.Verbose("Novel bluetooth device; creating new instance");
                device = new BluetoothDevice(
                    alias ?? name ?? address,
                    address,
                    connected: connected,
                    paired: paired,
                    trusted: trusted);

                bluezDevice.Disconnected += this.DeviceDisconnected;

                this.knownDevices[address] = (bluezDevice, device);
            }

            logger.Information("Device state is {@Device}", device);

            return device;
        }

        private async Task DeviceDisconnected(Device sender, BlueZEventArgs eventArgs)
        {
            await this.RefreshDeviceStateAsync(sender);
            this.OnDeviceDisconnected();
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
            this.OnDeviceDiscovered(await this.GetDeviceInfoAsync(eventArgs.Device));
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

        public override async Task ForgetDeviceAsync(BluetoothDevice device, CancellationToken cancellationToken)
        {
            if (this.knownDevices.TryGetValue(device.Address, out var deviceInfo))
            {
                var adapter = await this.GetAdapterAsync();
                if (adapter != null)
                {
                    try
                    {
                        await adapter.RemoveDeviceAsync(deviceInfo.bluezDevice.ObjectPath);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error removing device");
                    }
                }
            }
            else
            {
                // Log the fact that the device isn't tracked
                logger.Error("Device {Address} not in known devices list", device.Address);
            }

            await this.RefreshDeviceStateAsync(device);
        }
    }
}
