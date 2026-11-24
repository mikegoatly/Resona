using System.Reactive.Subjects;

using Android.Content;
using Android.Media;
using Android.Util;

using Resona.Services.Audio;

namespace Resona.Android.Platform;

/// <summary>
/// Android routes audio output itself (wired/USB, then Bluetooth, then the built-in speaker), so this only reports
/// which outputs are available and which one the system is using; choosing one is not supported.
/// </summary>
internal sealed class AndroidAudioOutputService : IAudioOutputService
{

    private readonly AudioManager audioManager;
    private readonly ReplaySubject<IReadOnlyList<AudioDevice>> audioOutputs = new(1);

    public AndroidAudioOutputService(Context context)
    {
        this.audioManager = (AudioManager)context.GetSystemService(Context.AudioService)!;
        this.audioManager.RegisterAudioDeviceCallback(new DeviceCallback(this), null);
        this.Refresh();
    }

    public IObservable<IReadOnlyList<AudioDevice>> AudioOutputs => this.audioOutputs;

    public Task SetActiveDeviceAsync(AudioDevice device, CancellationToken cancellationToken)
    {
        Log.Info(AndroidLog.Tag, $"Audio output selection is automatic on Android; ignoring request for {device.FriendlyName}");
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        var devices = (this.audioManager.GetDevices(GetDevicesTargets.Outputs) ?? [])
            .Select(ToAudioDevice)
            .OfType<AudioDevice>()
            .ToList();

        var active = devices.OrderBy(d => d.Kind switch
        {
            AudioDeviceKind.AudioOut => 0,
            AudioDeviceKind.Bluetooth => 1,
            _ => 2,
        }).FirstOrDefault();
        if (active != null)
        {
            active.Active = true;
        }

        Log.Info(AndroidLog.Tag, $"Audio outputs: {string.Join(", ", devices.Select(d => d.FriendlyName + (d.Active ? "*" : "")))}");
        this.audioOutputs.OnNext(devices);
    }

    private static AudioDevice? ToAudioDevice(AudioDeviceInfo info)
    {
        var kind = info.Type switch
        {
            AudioDeviceType.BuiltinSpeaker => AudioDeviceKind.Speaker,
            AudioDeviceType.BluetoothA2dp => AudioDeviceKind.Bluetooth,
            AudioDeviceType.WiredHeadphones or AudioDeviceType.WiredHeadset or AudioDeviceType.UsbDevice
                or AudioDeviceType.LineAnalog or AudioDeviceType.LineDigital
                or AudioDeviceType.Hdmi => AudioDeviceKind.AudioOut,
            // UsbHeadset only exists from API 26
            _ when OperatingSystem.IsAndroidVersionAtLeast(26) && info.Type == AudioDeviceType.UsbHeadset => AudioDeviceKind.AudioOut,
            _ => AudioDeviceKind.Undefined, // earpiece, telephony, SCO, ...: not music outputs
        };
        if (kind == AudioDeviceKind.Undefined)
        {
            return null;
        }

        var productName = info.ProductNameFormatted?.ToString();
        var name = string.IsNullOrWhiteSpace(productName) ? info.Type.ToString() : productName;
        var mac = kind == AudioDeviceKind.Bluetooth && OperatingSystem.IsAndroidVersionAtLeast(28) && !string.IsNullOrEmpty(info.Address)
            ? info.Address
            : null;

        return new AudioDevice(info.Id, mac, name, kind)
        {
            FriendlyName = kind == AudioDeviceKind.Speaker ? "Speaker" : name,
        };
    }

    private sealed class DeviceCallback(AndroidAudioOutputService owner) : AudioDeviceCallback
    {
        public override void OnAudioDevicesAdded(AudioDeviceInfo[]? addedDevices) => owner.Refresh();

        public override void OnAudioDevicesRemoved(AudioDeviceInfo[]? removedDevices) => owner.Refresh();
    }
}
