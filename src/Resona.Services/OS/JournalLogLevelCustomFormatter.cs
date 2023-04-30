using Serilog.Events;
using Serilog.Formatting;

namespace Resona.Services.OS
{
    internal class JournalLogLevelCustomFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            // https://wiki.archlinux.org/title/Systemd/Journal
            var journalPriority = logEvent.Level switch
            {
                LogEventLevel.Verbose => 7, // Debug
                LogEventLevel.Debug => 6, // Informational
                LogEventLevel.Information => 5, // Notice
                LogEventLevel.Warning => 4, // Warning
                LogEventLevel.Error => 3, // Error
                LogEventLevel.Fatal => 2, // Critical
                _ => throw new NotImplementedException("Unknown LogEventLevel " + logEvent.Level),
            };

            output.WriteLine($"<{journalPriority}> {logEvent.RenderMessage()}");
            if (logEvent.Exception != null)
            {
                output.WriteLine(logEvent.Exception.ToString());
            }
        }
    }
}
