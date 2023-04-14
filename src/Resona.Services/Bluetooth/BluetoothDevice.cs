namespace Resona.Services.Bluetooth
{
    public class BluetoothDevice
    {
        public BluetoothDevice(string name, string address, bool connected = false)
        {
            this.Name = name;
            this.Address = address;
            this.Connected = connected;
        }

        public string Name { get; }
        public string Address { get; }
        public bool Connected { get; set; }
    }
}
