using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Resona.Services.OS
{
    public abstract class BaseLogService : ILogService
    {
        protected BaseLogService()
        {
            Settings.Default.SettingsChanged += this.OnSettingsChanged;

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
            return Settings.Default.LogLevel;
        }

        private void OnSettingsChanged()
        {
            this.LoggingLevelSwitch.MinimumLevel = this.GetCurrentLogLevel();
        }

    }
}