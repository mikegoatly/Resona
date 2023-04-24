using System.ComponentModel;

namespace Resona.Services.Bluetooth
{
    public enum DeviceStatus
    {
        NotConnected = 0,
        Connected = 1,
        Connecting = 2
    }

    public class BluetoothDevice : INotifyPropertyChanged
    {
        private DeviceStatus status;
        public BluetoothDevice(string name, string address, bool connected = false)
        {
            this.Name = name;
            this.Address = address;
            this.Status = connected ? DeviceStatus.Connected : DeviceStatus.NotConnected;
        }

        public string Name { get; }
        public string Address { get; }
        public DeviceStatus Status
        {
            get => this.status;
            set
            {
                this.status = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Status)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
