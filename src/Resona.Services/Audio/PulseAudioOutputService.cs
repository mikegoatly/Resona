﻿using System.Collections.Concurrent;
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
            logger.Verbose("Reading list of Pulse Audio devices");

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
                    device.FriendlyName = await GetNameFromMacAsync(device.Mac, cancellationToken);
                }
            }

            // Special case - if no audio devices are selected then we pick the first one in the list and activate it.
            if (devices.Any(x => x.Active) == false && devices.Count > 0)
            {
                logger.Information("No active audio device detected; switching to the default one");
                await this.SetActiveDeviceAsync(devices[0], cancellationToken);
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
                    await this.SetActiveDeviceAsync(remainingOutputs[0], default);
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
                if (channelNumber > 1 && !mac.Success)
                {
                    logger.Error("MISSING BLUETOOTH MAC!");
                    result = default;
                    return false;
                }

                result = new AudioDevice(
                    channelNumber,
                    channelNumber switch
                    {
                        < 2 => null,
                        _ => mac.Value.Replace('_', ':')
                    },
                    match.Groups["Name"].Value,
                    channelNumber switch
                    {
                        0 => AudioDeviceKind.Speaker,
                        1 => AudioDeviceKind.AudioOut,
                        _ => AudioDeviceKind.Bluetooth
                    })
                {
                    FriendlyName = channelNumber switch
                    {
                        0 => "Speaker",
                        1 => "Audio out",
                        _ => match.Groups["Name"].Value
                    }
                };

                return true;
            }

            result = default;
            return false;
        }
    }
}
