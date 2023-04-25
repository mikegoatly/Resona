using System.Reactive.Subjects;

namespace Resona.Services.Audio
{
    public class DevAudioOutputService : IAudioOutputService
    {
        private readonly AudioDevice[] devices = new[]
        {
            new AudioDevice(0, null, "Speakers", AudioDeviceKind.Speaker),
            new AudioDevice(1, null, "Speakers", AudioDeviceKind.AudioOut),
            new AudioDevice(3, null, "Taotronics", AudioDeviceKind.Bluetooth) { Active = true }
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

            this.audioDevices.OnNext(this.devices);

            return Task.CompletedTask;
        }
    }
}
