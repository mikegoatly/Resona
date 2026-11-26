using System.Text.Json;
using System.Text.Json.Serialization;

using Serilog.Events;

namespace Resona.Services
{
    internal record SettingsData(
        LogEventLevel? LogEventLevel = null,
        TimeSpan? ScreenDimTimeout = null,
        TimeSpan? InactivityShutdownTimeout = null,
        bool? HostWebClient = null,
        bool? SpotifyEnabled = null,
        string? SpotifyClientId = null,
        string? SpotifyDeviceName = null);

    [JsonSerializable(typeof(SettingsData))]
    internal partial class SettingsSerializationContext : JsonSerializerContext { }

    public sealed class Settings
    {
        private const string SettingsFileName = "Resona.settings";

        private static readonly string DefaultSpotifyDeviceName = "Resona-" + Environment.MachineName;

        private static readonly Lock defaultLock = new();
        private static ResonaOptions? configuredOptions;
        private static Settings? defaultInstance;

        private readonly string settingsFilePath;

        private Settings(ResonaOptions options)
        {
            Options = options;
            settingsFilePath = Path.Combine(options.SettingsDirectory, SettingsFileName);

            var root = options.LibraryRoot.TrimEnd('/', '\\');
            MusicFolder = $"{root}/music";
            AudiobooksFolder = $"{root}/audiobooks";
            SleepFolder = $"{root}/sleep";
        }

        /// <summary>
        /// Reads the settings file from <see cref="ResonaOptions.SettingsDirectory"/> if present, otherwise returns defaults.
        /// </summary>
        public static Settings Load(ResonaOptions options)
        {
            var settings = new Settings(options);
            SettingsData? settingsData = null;

            if (File.Exists(settings.settingsFilePath))
            {
                using var stream = File.OpenRead(settings.settingsFilePath);
                settingsData = JsonSerializer.Deserialize(stream, SettingsSerializationContext.Default.SettingsData);
            }

            settings.LogLevel = settingsData?.LogEventLevel ?? LogEventLevel.Warning;
            settings.ScreenDimTimeout = settingsData?.ScreenDimTimeout ?? TimeSpan.FromSeconds(30);
            settings.InactivityShutdownTimeout = settingsData?.InactivityShutdownTimeout ?? TimeSpan.FromHours(2);
            settings.HostWebClient = settingsData?.HostWebClient ?? options.HostWebClientDefault;
            settings.SpotifyEnabled = settingsData?.SpotifyEnabled ?? false;
            settings.SpotifyClientId = settingsData?.SpotifyClientId;
            settings.SpotifyDeviceName = settingsData?.SpotifyDeviceName ?? DefaultSpotifyDeviceName;
            return settings;
        }

        /// <summary>
        /// Sets the options used by <see cref="Default"/>. Must be called before <see cref="Default"/> is first accessed;
        /// throws otherwise so ordering bugs are loud rather than silently using the wrong paths.
        /// </summary>
        public static void Configure(ResonaOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            lock (defaultLock)
            {
                if (defaultInstance is not null)
                {
                    throw new InvalidOperationException("Settings.Default has already been created; Settings.Configure must be called before it is first accessed.");
                }

                configuredOptions = options;
            }
        }

        /// <summary>
        /// The shared settings, created on first access from the options passed to <see cref="Configure"/>
        /// (or <c>new ResonaOptions()</c> if never configured, e.g. in the designer).
        /// </summary>
        public static Settings Default
        {
            get
            {
                lock (defaultLock)
                {
                    return defaultInstance ??= Load(configuredOptions ?? new ResonaOptions());
                }
            }
        }

        /// <summary>Test seam: forgets the configured options and the Default instance.</summary>
        internal static void ResetDefaultForTesting()
        {
            lock (defaultLock)
            {
                configuredOptions = null;
                defaultInstance = null;
            }
        }

        public Action? SettingsChanged { get; set; }

        public ResonaOptions Options { get; }

        public string MusicFolder { get; }
        public string AudiobooksFolder { get; }
        public string SleepFolder { get; }

        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Warning;

        public TimeSpan ScreenDimTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan InactivityShutdownTimeout { get; set; } = TimeSpan.FromHours(2);

        public bool HostWebClient { get; set; } = false;

        public bool SpotifyEnabled { get; set; } = false;

        public string? SpotifyClientId { get; set; }

        public string SpotifyDeviceName { get; set; } = DefaultSpotifyDeviceName;

        public void Save()
        {
            var settingsData = new SettingsData(
                this.LogLevel,
                ScreenDimTimeout: this.ScreenDimTimeout,
                InactivityShutdownTimeout: this.InactivityShutdownTimeout,
                HostWebClient: this.HostWebClient,
                SpotifyEnabled: this.SpotifyEnabled,
                SpotifyClientId: this.SpotifyClientId,
                SpotifyDeviceName: this.SpotifyDeviceName);

            var jsonString = JsonSerializer.Serialize(
                settingsData,
                SettingsSerializationContext.Default.SettingsData);

            Directory.CreateDirectory(Options.SettingsDirectory);
            File.WriteAllText(settingsFilePath, jsonString);
            this.SettingsChanged?.Invoke();
        }
    }
}
