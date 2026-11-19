namespace Resona.Services.Tests.Support;

/// <summary>
/// A throwaway directory standing in for the settings and image directories, so tests never touch the real AppData.
/// </summary>
public sealed class TempSettingsDirectory : IDisposable
{
    public TempSettingsDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "resona-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string SettingsFile => System.IO.Path.Combine(Path, "Resona.settings");

    /// <summary>Options with every directory pointed into this temp directory.</summary>
    public ResonaOptions Options(Func<ResonaOptions, ResonaOptions>? customize = null)
    {
        var options = new ResonaOptions
        {
            LibraryRoot = System.IO.Path.Combine(Path, "library"),
            SettingsDirectory = Path,
            CustomImageDirectory = System.IO.Path.Combine(Path, "app"),
        };
        return customize?.Invoke(options) ?? options;
    }

    public void WriteSettingsFile(string json) => File.WriteAllText(SettingsFile, json);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
