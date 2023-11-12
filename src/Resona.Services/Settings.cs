using System.Text.Json;
using System.Text.Json.Serialization;

using Serilog.Events;

namespace Resona.Services
{
    internal record SettingsData(
        LogEventLevel? LogEventLevel = null,
        TimeSpan? ScreenDimTimeout = null,
        TimeSpan? InactivityShutdownTimeout = null,
        bool? HostWebClient = null);

    [JsonSerializable(typeof(SettingsData))]
    internal partial class SettingsSerializationContext : JsonSerializerContext { }

    public sealed class Settings
    {
        private static readonly JsonSerializerOptions saveSerializationOptions = new() { WriteIndented = true };
        private static readonly FileInfo settingsFile = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Resona.settings"));

        public static Settings Load()
        {
            SettingsData? settingsData = null;

            if (settingsFile.Exists)
            {
                using var stream = settingsFile.OpenRead();
                settingsData = JsonSerializer.Deserialize(stream, SettingsSerializationContext.Default.SettingsData);
            }

            return new Settings
            {
                LogLevel = settingsData?.LogEventLevel ?? LogEventLevel.Warning,
                ScreenDimTimeout = settingsData?.ScreenDimTimeout ?? TimeSpan.FromSeconds(30),
                InactivityShutdownTimeout = settingsData?.InactivityShutdownTimeout ?? TimeSpan.FromHours(2),
                HostWebClient = settingsData?.HostWebClient ?? false,
            };
        }

        public Action? SettingsChanged { get; set; }

        public static Settings Default { get; } = Settings.Load();

        public string MusicFolder { get; } = "/home/pi/music";
        public string AudiobooksFolder { get; } = "/home/pi/audiobooks";
        public string SleepFolder { get; } = "/home/pi/sleep";

        public required LogEventLevel LogLevel { get; set; } = LogEventLevel.Warning;

        public required TimeSpan ScreenDimTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public required TimeSpan InactivityShutdownTimeout { get; set; } = TimeSpan.FromHours(2);

        public required bool HostWebClient { get; set; } = false;

        public void Save()
        {
            var settingsData = new SettingsData(
                this.LogLevel,
                ScreenDimTimeout: this.ScreenDimTimeout,
                InactivityShutdownTimeout: this.InactivityShutdownTimeout,
                HostWebClient: this.HostWebClient);

            var jsonString = JsonSerializer.Serialize(
                settingsData,
                SettingsSerializationContext.Default.SettingsData);

            File.WriteAllText(settingsFile.FullName, jsonString);
        }
    }
}