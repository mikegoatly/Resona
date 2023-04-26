namespace Resona.Services.Audio
{
    public record AudioDevice(int Id, string? Mac, string Name, AudioDeviceKind Kind)
    {
        public bool Active { get; set; }
        public required string FriendlyName { get; set; }
    }
}
