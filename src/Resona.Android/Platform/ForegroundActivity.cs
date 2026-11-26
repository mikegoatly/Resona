using Android.App;
using Android.OS;

namespace Resona.Android.Platform;

/// <summary>
/// Tracks Resona's currently resumed activity. Some SDKs (e.g. Spotify App Remote's authorization view) can only
/// show their own screens when started from an activity rather than the application context.
/// </summary>
internal sealed class ForegroundActivity : Java.Lang.Object, Application.IActivityLifecycleCallbacks
{
    private static WeakReference<Activity>? current;

    /// <summary>The resumed activity, or null when Resona isn't in the foreground.</summary>
    public static Activity? Current => current is not null && current.TryGetTarget(out var activity) && !activity.IsFinishing
        ? activity
        : null;

    public static void Register(Application application) => application.RegisterActivityLifecycleCallbacks(new ForegroundActivity());

    public void OnActivityResumed(Activity activity) => current = new WeakReference<Activity>(activity);

    public void OnActivityPaused(Activity activity)
    {
        if (Current == activity)
        {
            current = null;
        }
    }

    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState) { }

    public void OnActivityStarted(Activity activity) { }

    public void OnActivityStopped(Activity activity) { }

    public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }

    public void OnActivityDestroyed(Activity activity) { }
}
