using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

using DynamicData;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Bluetooth;

using Serilog;

namespace Resona.UI.ViewModels
{
    public class BluetoothSettingsViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<ObservableCollection<BluetoothDevice>> bluetoothDevices;
        private static readonly ILogger logger = Log.ForContext<BluetoothSettingsViewModel>();
        private readonly IBluetoothService bluetoothService;

#if DEBUG
        public BluetoothSettingsViewModel()
            : this(new DevBluetoothService())
        {
        }
#endif

        public BluetoothSettingsViewModel(
            IBluetoothService bluetoothService)
        {
            this.bluetoothService = bluetoothService;

            this.bluetoothService.ScanningStateChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.ScanningStateChanged);

            this.bluetoothService.BluetoothDeviceDiscovered
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.BluetoothDeviceDiscovered);

            this.RefreshDeviceList = ReactiveCommand.CreateFromTask(
                this.bluetoothService.StartScanningAsync,
                this.WhenAnyValue(x => x.IsScanning, x => x == false));

            this.bluetoothDevices = Observable
                .FromAsync(this.bluetoothService.GetKnownDevicesAsync)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(x => new ObservableCollection<BluetoothDevice>(x))
                .ToProperty(this, x => x.BluetoothDevices);

            this.ConnectDeviceCommand = ReactiveCommand.CreateFromTask<BluetoothDevice>(
                x => this.bluetoothService.ConnectAsync(x, default));

            // Trigger the refresh command immediately to populate the list
            this.RefreshDeviceList.Execute().Subscribe().Dispose();
        }

        private void BluetoothDeviceDiscovered(BluetoothDevice device)
        {
            // This is running on the main thread, so we can safely modify the collection
            var duplicate = this.BluetoothDevices.FirstOrDefault(x => x.Address == device.Address);
            if (duplicate != null)
            {
                logger.Debug("Replacing duplicate detected device with address {Address}", device.Address);
                this.BluetoothDevices.Replace(duplicate, device);
            }
            else
            {
                this.BluetoothDevices.Add(device);
            }
        }

        public void ScanningStateChanged(bool isScanning)
        {
            this.IsScanning = isScanning;
        }

        public ReactiveCommand<BluetoothDevice, Unit> ConnectDeviceCommand { get; }
        public ObservableCollection<BluetoothDevice> BluetoothDevices => this.bluetoothDevices.Value;

        [Reactive]
        public bool IsScanning { get; set; }
        public ReactiveCommand<Unit, Unit> RefreshDeviceList { get; }
    }
}
