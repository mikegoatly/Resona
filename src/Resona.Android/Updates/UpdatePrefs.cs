using Android.Content;

namespace Resona.Android.Updates;

/// <summary>Persisted update settings and last-check status (read back by the script's Status action).</summary>
internal static class UpdatePrefs
{
    private const string PrefsName = "resona_update";
    private const string UrlKey = "url";
    private const string LastCheckKey = "last_check_utc";
    private const string LastResultKey = "last_result";

    private static ISharedPreferences Prefs(Context context) => context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;

    public static string? GetUrl(Context context)
    {
        var url = Prefs(context).GetString(UrlKey, null);
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    public static void SetUrl(Context context, string? url)
        => Prefs(context).Edit()!.PutString(UrlKey, url?.Trim() ?? string.Empty)!.Commit();

    public static void RecordResult(Context context, string result)
        => Prefs(context).Edit()!
            .PutString(LastCheckKey, DateTime.UtcNow.ToString("O"))!
            .PutString(LastResultKey, result)!
            .Commit();

    public static (string? Time, string? Result) GetLast(Context context)
    {
        var prefs = Prefs(context);
        return (prefs.GetString(LastCheckKey, null), prefs.GetString(LastResultKey, null));
    }
}
