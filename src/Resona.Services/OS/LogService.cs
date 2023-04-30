using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Resona.Services.OS
{
    public interface ILogService
    {
        Task<float?> GetLogSizeAsyncMb();
    }

    public class LogService : ILogService
    {
        // Regext to match a number using the current culture
        private static readonly Regex defaultSinkRegex = new(@"take up (?<SizeMb>[^\w]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly LoggingLevelSwitch loggingLevelSwitch;

        private readonly ILogger logger = Log.ForContext<LogService>();

        public LogService()
        {
            Settings.Default.SettingsSaving += this.OnSettingsChanged;

            this.loggingLevelSwitch = new LoggingLevelSwitch(this.GetCurrentLogLevel());

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(this.loggingLevelSwitch)
                .Enrich.FromLogContext()
                .WriteTo.Console(new JournalLogLevelCustomFormatter())
                .CreateLogger();
        }

        public async Task<float?> GetLogSizeAsyncMb()
        {
            return (await BashExecutor.ExecuteAsync<float?>(
                "journalctl --user -u Resona.service --disk-usage",
                this.ProcessDiskUsageLine,
                default)).FirstOrDefault();
        }

        private void OnSettingsChanged(object sender, CancelEventArgs e)
        {
            this.loggingLevelSwitch.MinimumLevel = this.GetCurrentLogLevel();
        }

        private LogEventLevel GetCurrentLogLevel()
        {
            var parsedLogLevel = Enum.TryParse<LogEventLevel>(Settings.Default.LogLevel, out var logLevel);
            if (!parsedLogLevel)
            {
                logLevel = LogEventLevel.Warning;
                this.logger.Error("Unable to parse log level {LogLevel} - defaulting to Warning", Settings.Default.LogLevel);
            }

            return logLevel;
        }

        private bool ProcessDiskUsageLine(string line, [NotNullWhen(true)] out float? result)
        {
            var match = defaultSinkRegex.Match(line);
            if (match.Success)
            {
                var value = match.Groups["SizeMb"].Value;
                if (float.TryParse(value, CultureInfo.CurrentCulture, out var sizeMb))
                {
                    result = sizeMb;
                    return true;
                }
                else
                {
                    this.logger.Warning("Unable to parse log size from value {RawValue} - line was {LineText}", match.Groups["SizeMb"].Value, line);
                }
            }

            result = default;
            return false;
        }
    }
}
