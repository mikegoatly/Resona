namespace Resona.Services
{
    /// <summary>
    /// Platform-specific configuration supplied by each head (Pi/desktop, Android, ...). The defaults match the
    /// Raspberry Pi deployment.
    /// </summary>
    public sealed record ResonaOptions
    {
        /// <summary>The directory containing the music, audiobooks and sleep library folders.</summary>
        public string LibraryRoot { get; init; } = "/home/pi";

        /// <summary>The directory holding the Resona.settings file.</summary>
        public string SettingsDirectory { get; init; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        /// <summary>The (writable) directory under which custom library icons are stored, in an images/ subfolder.</summary>
        public string CustomImageDirectory { get; init; } = AppContext.BaseDirectory;

        /// <summary>Whether the web client is hosted when the settings file doesn't say either way.</summary>
        public bool HostWebClientDefault { get; init; } = false;

        /// <summary>The host part of the web host's HttpListener prefix ("*" binds all interfaces).</summary>
        public string WebBindHost { get; init; } = "*";

        /// <summary>When true, the bootstrapper creates the music, audiobooks and sleep folders at startup.</summary>
        public bool EnsureLibraryFolders { get; init; } = false;
    }
}
