namespace Resona.Services.Bluetooth
{
    public class AudioDevice
    {
        public AudioDevice(int id, string? mac, string name, AudioDeviceKind kind)
        {
            Id = id;
            Mac = mac;
            Name = name;
            Kind = kind;
        }

        public int Id { get; }
        public string? Mac { get; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public AudioDeviceKind Kind { get; }
    }
}
