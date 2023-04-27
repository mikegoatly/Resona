using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Bluetooth;

using Serilog;

namespace Resona.UI.ViewModels
{
    public class BluetoothSettingsViewModel : ReactiveObject
    {
        private readonly IBluetoothService bluetoothService;
        private static readonly ILogger logger = Log.ForContext<BluetoothSettingsViewModel>();

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

            this.RefreshDeviceList = ReactiveCommand.CreateFromTask(
                this.bluetoothService.StartScanningAsync,
                this.WhenAnyValue(x => x.IsScanning, x => x == false));

            var task = this.ResyncDeviceListAsync();


            this.bluetoothService.ScanningStateChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.ScanningStateChanged);

            this.bluetoothService.BluetoothDeviceDiscovered
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.BluetoothDeviceDiscovered);

            this.bluetoothService.BluetoothDeviceDisconnected
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.BluetoothDeviceDisconnected);

            this.ConnectDeviceCommand = ReactiveCommand.CreateFromTask<BluetoothDevice>(
                x => this.bluetoothService.ConnectAsync(x, default));
        }

        private async void BluetoothDeviceDisconnected(Unit unit)
        {
            await this.ResyncDeviceListAsync();
        }

        private async void BluetoothDeviceDiscovered(BluetoothDevice device)
        {
            await this.ResyncDeviceListAsync();
        }

        private async Task ResyncDeviceListAsync()
        {
            this.BluetoothDevices.Clear();

            foreach (var device in await this.bluetoothService.GetKnownDevicesAsync(default))
            {
                this.BluetoothDevices.Add(device);
            }
        }

        public void ScanningStateChanged(bool isScanning)
        {
            this.IsScanning = isScanning;
        }

        public ReactiveCommand<BluetoothDevice, Unit> ConnectDeviceCommand { get; }
        public ObservableCollection<BluetoothDevice> BluetoothDevices { get; } = new ObservableCollection<BluetoothDevice>();

        [Reactive]
        public bool IsScanning { get; set; }
        public ReactiveCommand<Unit, Unit> RefreshDeviceList { get; }
    }
}
