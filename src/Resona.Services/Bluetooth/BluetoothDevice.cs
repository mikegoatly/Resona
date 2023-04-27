using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        private string name;
        private bool paired;
        private bool trusted;

        public BluetoothDevice(string name, string address, bool connected = false, bool paired = false, bool trusted = false)
        {
            this.name = name;
            this.Address = address;
            this.Paired = paired;
            this.Status = connected ? DeviceStatus.Connected : DeviceStatus.NotConnected;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => this.name;
            set => this.SetAndRaiseOnPropertyChanged(ref this.name, value);
        }

        public string Address { get; }

        public bool Paired
        {
            get => this.paired;
            set => this.SetAndRaiseOnPropertyChanged(ref this.paired, value);
        }

        public bool Trusted
        {
            get => this.trusted;
            set => this.SetAndRaiseOnPropertyChanged(ref this.trusted, value);
        }

        public DeviceStatus Status
        {
            get => this.status;
            set => this.SetAndRaiseOnPropertyChanged(ref this.status, value);
        }

        private void SetAndRaiseOnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Comparer<T>.Default.Compare(field, value) != 0)
            {
                field = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
