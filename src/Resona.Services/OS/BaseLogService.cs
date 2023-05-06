using System.ComponentModel;
using System.Diagnostics;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Resona.Services.OS
{
    public abstract class BaseLogService : ILogService
    {
        protected BaseLogService()
        {
            Settings.Default.SettingsSaving += this.OnSettingsChanged;

            this.LoggingLevelSwitch = new LoggingLevelSwitch(this.GetCurrentLogLevel());

            var loggingConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(this.LoggingLevelSwitch)
                .Enrich.FromLogContext();

            loggingConfig = this.ConfigureLogOutput(loggingConfig);

            Log.Logger = loggingConfig.CreateLogger();
        }

        protected LoggingLevelSwitch LoggingLevelSwitch { get; private set; }

        protected abstract LoggerConfiguration ConfigureLogOutput(LoggerConfiguration loggingConfig);

        public abstract Task<float?> GetLogSizeMbAsync();

        public abstract Task ClearLogsAsync();

        private LogEventLevel GetCurrentLogLevel()
        {
            var parsedLogLevel = Enum.TryParse<LogEventLevel>(Settings.Default.LogLevel, out var logLevel);
            if (!parsedLogLevel)
            {
                logLevel = LogEventLevel.Warning;
                Trace.WriteLine("Unable to parse log level {LogLevel} - defaulting to Warning", Settings.Default.LogLevel);
            }

            return logLevel;
        }

        private void OnSettingsChanged(object sender, CancelEventArgs e)
        {
            this.LoggingLevelSwitch.MinimumLevel = this.GetCurrentLogLevel();
        }

    }
}