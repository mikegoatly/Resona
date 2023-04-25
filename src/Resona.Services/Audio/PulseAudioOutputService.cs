using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;

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

    public class PulseAudioOutputService : IAudioOutputService
    {
        private static readonly ILogger logger = Log.ForContext<PulseAudioOutputService>();

        private static readonly Regex bluezSinkOutputRegex = new(@"^(?<Number>\d+)\s+(?<Name>(bluez_sink\.(?<Mac>[0-9a-f_]+)[^\s]+|[^\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex defaultSinkRegex = new("^Default Sink: (?<Name>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex getNameOutputRegex = new(@"^\s+Device name: (?<DeviceName>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly ConcurrentDictionary<string, string> deviceNameLookup = new(StringComparer.OrdinalIgnoreCase);

        private readonly ReplaySubject<Unit> refreshTrigger = new();

        public PulseAudioOutputService(IBluetoothService bluetoothService)
        {
            bluetoothService.BluetoothDeviceConnected.Subscribe(this.OnBluetoothDeviceConnected);

            // Update the audioOutputs collection when a new list is emitted.
            this.AudioOutputs = this.refreshTrigger
                .SelectMany(_ => Observable.FromAsync(this.ListAsync))
                .Replay(1)
                .RefCount();

            logger.Information("Starting PulseAudioOutputService");

            this.refreshTrigger.OnNext(Unit.Default);
        }

        public IObservable<IReadOnlyList<AudioDevice>> AudioOutputs { get; private set; }

        private async Task<IReadOnlyList<AudioDevice>> ListAsync(CancellationToken cancellationToken)
        {
            logger.Information("Reading list of devices");

            var devices = await BashExecutor.ExecuteAsync<AudioDevice>(
                "pactl list short sinks",
                ProcessListOutputLine,
                cancellationToken);

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
                    device.Name = await GetNameFromMacAsync(device.Mac, cancellationToken);
                }
            }

            return devices;
        }

        public async Task SetActiveDeviceAsync(AudioDevice device, CancellationToken cancellationToken)
        {
            logger.Information($"Setting active audio device to #{device.Id} - {device.Name}");

            await BashExecutor.ExecuteAsync(
                $"pactl set-default-sink {device.Id}",
                cancellationToken);

            // Refresh the available list of devices
            this.refreshTrigger.OnNext(Unit.Default);
        }

        private async void OnBluetoothDeviceConnected(BluetoothDevice device)
        {
            logger.Information("Attempting to switch audio output to {DeviceName}", device.Name);

            try
            {
                var audioOutputs = await this.ListAsync(default);
                var audioOutput = audioOutputs.FirstOrDefault(x => x.Mac == device.Address);
                if (audioOutput == null)
                {
                    logger.Information("Cannot find audio output with matching mac address - the device may not have audio output capabilities");
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
            if (match.Success && int.TryParse(match.Groups["Number"].Value, out var number) && number != 2)
            {
                var mac = match.Groups["Mac"];
                if (number > 1 && !mac.Success)
                {
                    logger.Error("MISSING BLUETOOTH MAC!");
                    result = default;
                    return false;
                }

                result = new AudioDevice(
                    number,
                    number switch
                    {
                        < 2 => null,
                        _ => mac.Value.Replace('_', ':')
                    },
                    number switch
                    {
                        0 => "Speaker",
                        1 => "Audio out",
                        _ => match.Groups["Name"].Value
                    },
                    number switch
                    {
                        0 => AudioDeviceKind.Speaker,
                        1 => AudioDeviceKind.AudioOut,
                        _ => AudioDeviceKind.Bluetooth
                    });

                return true;
            }

            result = default;
            return false;
        }
    }
}
