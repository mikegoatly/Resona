namespace Resona.Services.Audio
{
    public class AudioDevice
    {
        public AudioDevice(int id, string? mac, string name, AudioDeviceKind kind)
        {
            this.Id = id;
            this.Mac = mac;
            this.Name = name;
            this.Kind = kind;
        }

        public int Id { get; }
        public string? Mac { get; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public AudioDeviceKind Kind { get; }
    }
}
