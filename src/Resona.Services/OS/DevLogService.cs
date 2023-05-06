using Serilog;

namespace Resona.Services.OS
{
    public class DevLogService : BaseLogService
    {
        public override Task ClearLogsAsync()
        {
            return Task.CompletedTask;
        }

        public override Task<float?> GetLogSizeMbAsync()
        {
            return Task.FromResult<float?>(100F);
        }

        protected override LoggerConfiguration ConfigureLogOutput(LoggerConfiguration loggingConfig)
        {
            return loggingConfig.WriteTo.Debug();
        }
    }
}
