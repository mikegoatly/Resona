namespace Resona.Services.Bluetooth
{
    /// <summary>Platform-neutral helpers used by platform Bluetooth implementations that work from device classes.</summary>
    public static class BluetoothDeviceFilter
    {
        /// <summary>The Bluetooth "Audio/Video" major device class (headphones, speakers, headsets, ...).</summary>
        public const int AudioVideoMajorClass = 0x0400;

        /// <summary>
        /// True when a device could be an audio sink: its major class is Audio/Video, or the class is unknown
        /// (devices that don't advertise one, which includes many cheap speakers).
        /// </summary>
        public static bool IsAudioCandidate(int? majorClass)
            => majorClass is null || majorClass == AudioVideoMajorClass;

        public static string DisplayName(string? name, string address)
            => string.IsNullOrWhiteSpace(name) ? address : name;
    }
}
