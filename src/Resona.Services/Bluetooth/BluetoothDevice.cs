namespace Resona.Services.Bluetooth
{
    public class BluetoothDevice
    {
        public BluetoothDevice(string name, string address, bool connected = false)
        {
            Name = name;
            Address = address;
            Connected = connected;
        }

        public string Name { get; }
        public string Address { get; }
        public bool Connected { get; set; }
    }
}
