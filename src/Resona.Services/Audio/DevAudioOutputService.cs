using System.Reactive.Subjects;

namespace Resona.Services.Audio
{
    public class DevAudioOutputService : IAudioOutputService
    {
        private readonly AudioDevice[] devices = new[]
        {
            new AudioDevice(0, null, "Speakers", AudioDeviceKind.Speaker) { FriendlyName ="Speakers"},
            new AudioDevice(1, null, "Audio Out", AudioDeviceKind.AudioOut) { FriendlyName ="Audio Out"},
            new AudioDevice(3, null, "Bluetooth headphones", AudioDeviceKind.Bluetooth) { Active = true, FriendlyName = "Bluetooth headphones" }
        };

        private readonly ReplaySubject<IReadOnlyList<AudioDevice>> audioDevices = new();

        public DevAudioOutputService()
        {
            this.audioDevices.OnNext(this.devices);
        }

        public IObservable<IReadOnlyList<AudioDevice>> AudioOutputs => this.audioDevices;

        public Task SetActiveDeviceAsync(AudioDevice device, CancellationToken cancellationToken)
        {
            foreach (var knownDevice in this.devices)
            {
                knownDevice.Active = device == knownDevice;
            }

            // ToList is a hack here - it tricks the UI into updating the list even though the list itself is the same
            this.audioDevices.OnNext(this.devices.ToList());

            return Task.CompletedTask;
        }
    }
}
