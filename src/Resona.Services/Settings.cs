using System.Text.Json;
using System.Text.Json.Serialization;

using Serilog.Events;

namespace Resona.Services
{
    [JsonSerializable(typeof(Dictionary<string, JsonElement>))]
    internal partial class SettingsSerializationContext : JsonSerializerContext { }

    public sealed class Settings
    {
        private static readonly JsonSerializerOptions saveSerializationOptions = new() { WriteIndented = true };
        private static readonly FileInfo settingsFile = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Resona.settings"));

        public static Settings Load()
        {
            Dictionary<string, JsonElement>? settingsDict = null;

            if (settingsFile.Exists)
            {
                using var stream = settingsFile.OpenRead();
                settingsDict = JsonSerializer.Deserialize(stream, SettingsSerializationContext.Default.DictionaryStringJsonElement);
            }

            settingsDict ??= new Dictionary<string, JsonElement>();

            T GetValue<T>(string key, T defaultValue)
            {
                if (settingsDict.TryGetValue(key, out var value))
                {
                    return value switch
                    {
                        T result => result,
                        { ValueKind: JsonValueKind.String } when typeof(T).IsEnum
                            && Enum.TryParse(typeof(T), value.GetString(), out var enumValue) => (T)enumValue,
                        { ValueKind: JsonValueKind.String } when typeof(T) == typeof(TimeSpan)
                            && TimeSpan.TryParse(value.GetString(), out var timeSpanValue) => (T)(object)timeSpanValue,
                        { ValueKind: JsonValueKind.False or JsonValueKind.True } when typeof(T) == typeof(bool)
                            => (T)(object)value.GetBoolean(),
                        _ => defaultValue,
                    };
                }

                // Key not found, return default value
                return defaultValue;
            }

            return new Settings
            {
                LogLevel = GetValue("LogLevel", LogEventLevel.Warning),
                ScreenDimTimeout = GetValue("ScreenDimTimeout", TimeSpan.FromSeconds(30)),
                InactivityShutdownTimeout = GetValue("InactivityShutdownTimeout", TimeSpan.FromHours(2)),
                HostWebClient = GetValue("HostWebClient", false),
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
            var settingsData = new Dictionary<string, object>
            {
                ["LogLevel"] = this.LogLevel.ToString(),
                ["ScreenDimTimeout"] = this.ScreenDimTimeout.ToString(),
                ["InactivityShutdownTimeout"] = this.InactivityShutdownTimeout.ToString(),
                ["HostWebClient"] = this.HostWebClient
            };

            var jsonString = JsonSerializer.Serialize(
                settingsData,
                typeof(Dictionary<string, JsonElement>),
                SettingsSerializationContext.Default);

            File.WriteAllText(settingsFile.FullName, jsonString);
        }
    }
}