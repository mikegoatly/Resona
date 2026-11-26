using Serilog.Core;
using Serilog.Events;

using AndroidLogger = Android.Util.Log;

namespace Resona.Android.Platform;

/// <summary>
/// Writes Serilog events to logcat under the Resona tag. Serilog's Debug sink is only visible in debug builds, so
/// without this the release (kiosk) app's own logging is lost.
/// </summary>
internal sealed class LogcatSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var source = logEvent.Properties.TryGetValue("SourceContext", out var context)
            ? context.ToString().Trim('"')
            : null;
        var message = logEvent.RenderMessage();
        if (source is not null)
        {
            // Just the class name keeps logcat lines readable
            message = $"[{source[(source.LastIndexOf('.') + 1)..]}] {message}";
        }

        if (logEvent.Exception is not null)
        {
            message += Environment.NewLine + logEvent.Exception;
        }

        switch (logEvent.Level)
        {
            case LogEventLevel.Verbose:
                AndroidLogger.Verbose(AndroidLog.Tag, message);
                break;
            case LogEventLevel.Debug:
                AndroidLogger.Debug(AndroidLog.Tag, message);
                break;
            case LogEventLevel.Information:
                AndroidLogger.Info(AndroidLog.Tag, message);
                break;
            case LogEventLevel.Warning:
                AndroidLogger.Warn(AndroidLog.Tag, message);
                break;
            default:
                AndroidLogger.Error(AndroidLog.Tag, message);
                break;
        }
    }
}
