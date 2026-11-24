using Resona.Services.OS;

using Serilog;

namespace Resona.Android.Platform;

/// <summary>
/// Serilog output goes to the debug output (logcat), which the OS owns and rotates, so there are no app log files
/// to measure or clear.
/// </summary>
internal sealed class AndroidLogService : BaseLogService
{
    public override Task<float?> GetLogSizeMbAsync() => Task.FromResult<float?>(0F);

    public override Task ClearLogsAsync() => Task.CompletedTask;

    protected override LoggerConfiguration ConfigureLogOutput(LoggerConfiguration loggingConfig)
        => loggingConfig.WriteTo.Debug();
}
