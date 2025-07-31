using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading;

using Resona.Services.Bluetooth;
using Resona.Services.OS;

using Serilog;

namespace Resona.Services.Audio
{
    public interface IAudioOutputService
    {
        IObservable<IReadOnlyList<AudioDevice>> AudioOutputs { get; }
        Task SetActiveDeviceAsync(AudioDevice device, CancellationToken cancellationToken);
    }

    public partial class PulseAudioOutputService : IAudioOutputService
    {
        private static readonly ILogger logger = Log.ForContext<PulseAudioOutputService>();

        private static readonly Regex bluezSinkOutputRegex = BluezSinkOutputRegex();
        private static readonly Regex defaultSinkRegex = DefaultSinkRegex();
        private static readonly Regex getNameOutputRegex = GetNameOutputRegex();

        private static readonly ConcurrentDictionary<string, string> deviceNameLookup = new(StringComparer.OrdinalIgnoreCase);

        private readonly ReplaySubject<Unit> refreshTrigger = new();

        public PulseAudioOutputService(IBluetoothService bluetoothService)
        {
            bluetoothService.BluetoothDeviceConnected.Subscribe(this.OnBluetoothDeviceConnected);
            bluetoothService.BluetoothDeviceDisconnected.Subscribe(this.OnBluetoothDeviceDisconnected);

            // Update the audioOutputs collection when a new list is emitted.
            this.AudioOutputs = this.refreshTrigger
                .SelectMany(_ => Observable.FromAsync(this.ListAsync))
                .Replay(1)
                .RefCount();

            this.refreshTrigger.OnNext(Unit.Default);

            logger.Verbose("Starting Pulse Audio service");
        }

        public IObservable<IReadOnlyList<AudioDevice>> AudioOutputs { get; private set; }

        private async Task<IReadOnlyList<AudioDevice>> ListAsync(CancellationToken cancellationToken)
        {
            var devices = await GetAudioDevicesAsync(cancellationToken);

            var activeDevice = (await BashExecutor.ExecuteAsync<string>(
                "pactl info",
                ProcessActiveDeviceInfoOutputLine,
                cancellationToken))
                .FirstOrDefault();

            foreach (var device in devices)
            {
                device.Active = string.Equals(activeDevice, device.Name, StringComparison.OrdinalIgnoreCase);

                if (device.Mac != null)
                {
                    device.FriendlyName = await GetNameFromMacAsync(device.Mac, cancellationToken);
                }
            }

            // Special case - if no audio devices are selected then we pick the first one in the list and activate it.
            if (devices.Any(x => x.Active) == false && devices.Count > 0)
            {
                logger.Information("No active audio device detected; switching to the speaker");
                await this.SwitchToSpeakerOutputAsync(devices, cancellationToken);
            }

            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug("Found {Count} audio devices", devices.Count);
                foreach (var device in devices)
                {
                    logger.Debug("   {Id} - {Name} - {Mac} - {Active} - {Kind}", device.Id, device.Name, device.Mac, device.Active, device.Kind);
                }
            }

            return devices;
        }

        public async Task SetActiveDeviceAsync(AudioDevice device, CancellationToken cancellationToken)
        {
            logger.Information($"Setting active audio device to #{device.Id} - {device.FriendlyName}");

            await BashExecutor.ExecuteAsync(
                $"pactl set-default-sink {device.Id}",
                cancellationToken);

            // Refresh the available list of devices
            this.refreshTrigger.OnNext(Unit.Default);
        }

        private async void OnBluetoothDeviceDisconnected(Unit unit)
        {
            try
            {
                var remainingOutputs = await this.ListAsync(default);

                // If after a bluetooth device disconnect the active output is the Audio Out, this is probably not what we want;
                // switching to the speaker is probably the best option.
                if (remainingOutputs.Any(x => x.Kind == AudioDeviceKind.AudioOut && x.Active))
                {
                    logger.Information("Switching to speaker after bluetooth disconnect");
                    await this.SwitchToSpeakerOutputAsync(remainingOutputs, default);
                }
                else
                {
                    // Just refresh the list of audio devices. This will switch over to speakers if needed.
                    this.refreshTrigger.OnNext(Unit.Default);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error refreshing audio device list after bluetooth disconnect");
            }
        }

        private async Task SwitchToSpeakerOutputAsync(IReadOnlyList<AudioDevice> devices, CancellationToken cancellationToken)
        {
            var speaker = devices.FirstOrDefault(x => x.Kind == AudioDeviceKind.Speaker);
            if (speaker != null)
            {
                await this.SetActiveDeviceAsync(speaker, cancellationToken);
            }
            else
            {
                logger.Warning("No speaker found; switching to the first device in the list");
                await this.SetActiveDeviceAsync(devices[0], cancellationToken);
            }
        }

        private static async Task<IReadOnlyList<AudioDevice>> GetAudioDevicesAsync(CancellationToken cancellationToken)
        {
            logger.Verbose("Reading list of Pulse Audio devices");

            IReadOnlyList<AudioDevice> devices;

            var retryCount = 0;
            do
            {
                devices = await BashExecutor.ExecuteAsync<AudioDevice>(
                    "pactl list short sinks",
                    ProcessListOutputLine,
                    cancellationToken);

                if (devices.Count < 2)
                {
                    logger.Warning("{Count} audio devices found; expected at least 2. Retrying in 1 second", devices.Count);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    retryCount++;
                }
            }
            while (devices.Count < 2 && retryCount < 60);

            if (devices.Count == 0)
            {
                logger.Error("No audio devices found after 5 retries");
            }

            return devices;
        }

        private async void OnBluetoothDeviceConnected(BluetoothDevice device)
        {
            logger.Information("Attempting to switch audio output to {DeviceName}, {Mac}", device.Name, device.Address);

            try
            {
                var audioOutputs = await this.ListAsync(default);
                var audioOutput = audioOutputs.FirstOrDefault(x => x.Mac == device.Address);
                if (audioOutput != null)
                {
                    logger.Information("Matched audio output on MAC address");
                }
                else
                {
                    audioOutput = audioOutputs.FirstOrDefault(x => x.FriendlyName == device.Name);
                    if (audioOutput != null)
                    {
                        logger.Information("Matched audio output on name");
                    }
                }

                if (audioOutput == null)
                {
                    logger.Warning("Cannot find audio output with matching mac address");
                }
                else
                {
                    await this.SetActiveDeviceAsync(audioOutput, default);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error switching audio device");
            }
        }

        private static async Task<string> GetNameFromMacAsync(string mac, CancellationToken cancellationToken)
        {
            if (deviceNameLookup.TryGetValue(mac, out var name))
            {
                return name;
            }

            static bool ProcessGetNameOutputLine(string line, [NotNullWhen(true)] out string? result)
            {
                var match = getNameOutputRegex.Match(line);
                if (match.Success)
                {
                    result = match.Groups["DeviceName"].Value;
                    return true;
                }

                result = null;
                return false;
            }

            var result = (await BashExecutor.ExecuteAsync<string>(
                $"hcitool info {mac}",
                ProcessGetNameOutputLine,
                cancellationToken)).FirstOrDefault(mac);

            deviceNameLookup.TryAdd(mac, result);
            return result;
        }

        private static bool ProcessActiveDeviceInfoOutputLine(string line, [NotNullWhen(true)] out string? result)
        {
            var match = defaultSinkRegex.Match(line);
            if (match.Success)
            {
                result = match.Groups["Name"].Value;
                return true;
            }

            result = default;
            return false;
        }

        private static bool ProcessListOutputLine(string line, [NotNullWhen(true)] out AudioDevice? result)
        {
            var match = bluezSinkOutputRegex.Match(line);
            if (match.Success && int.TryParse(match.Groups["Number"].Value, out var channelNumber))
            {
                var mac = match.Groups["Mac"];
                var hasMac = mac.Success;
                var kind = match.Groups["Name"].Value switch
                {
                    var name when name.StartsWith("bluez_sink.", StringComparison.OrdinalIgnoreCase) => AudioDeviceKind.Bluetooth,
                    var name when name.Contains("analog-stereo", StringComparison.OrdinalIgnoreCase) => AudioDeviceKind.AudioOut,
                    var name when name.Contains("digital-stereo", StringComparison.OrdinalIgnoreCase) => AudioDeviceKind.Speaker,
                    _ => AudioDeviceKind.Undefined
                };

                if (kind == AudioDeviceKind.Undefined)
                {
                    Log.Warning("Unknown audio device: {Line}", line);
                    result = default;
                    return false;
                }

                result = new AudioDevice(
                    channelNumber,
                    hasMac ? mac.Value.Replace('_', ':') : null,
                    match.Groups["Name"].Value,
                    kind)
                {
                    FriendlyName = kind switch
                    {
                        AudioDeviceKind.Speaker => "Speaker",
                        AudioDeviceKind.AudioOut => "Audio out",
                        _ => match.Groups["Name"].Value
                    }
                };

                return true;
            }

            result = default;
            return false;
        }

        [GeneratedRegex(@"^(?<Number>\d+)\s+(?<Name>(bluez_sink\.(?<Mac>[0-9a-f_]+)[^\s]+|[^\s]+))", RegexOptions.IgnoreCase)]
        private static partial Regex BluezSinkOutputRegex();
        [GeneratedRegex(@"^\s+Device name: (?<DeviceName>.*)$", RegexOptions.IgnoreCase)]
        private static partial Regex GetNameOutputRegex();
        [GeneratedRegex("^Default Sink: (?<Name>.+)$", RegexOptions.IgnoreCase)]
        private static partial Regex DefaultSinkRegex();
    }
}
