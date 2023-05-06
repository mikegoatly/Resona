using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

using Serilog;

namespace Resona.Services.OS
{
    public interface ILogService
    {
        Task ClearLogsAsync();
        Task<float?> GetLogSizeMbAsync();
    }

    public class LinuxLogService : BaseLogService
    {
        private static readonly Regex defaultSinkRegex = new(@"take up (?<SizeMb>[^a-z]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public override async Task<float?> GetLogSizeMbAsync()
        {
            return (await BashExecutor.ExecuteAsync<float?>(
                "journalctl --user -u Resona.service --disk-usage",
                this.ProcessDiskUsageLine,
                default)).FirstOrDefault();
        }

        public override async Task ClearLogsAsync()
        {
            await BashExecutor.ExecuteAsync(
                "sudo journalctl --user -u Resona.service --rotate && sudo journalctl --user -u Resona.service --vacuum-size=1M",
                default);
        }

        protected override LoggerConfiguration ConfigureLogOutput(LoggerConfiguration loggingConfig)
        {
            return loggingConfig.WriteTo.Console(new JournalLogLevelCustomFormatter());
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
                    Trace.TraceWarning("Unable to parse log size from value {RawValue} - line was {LineText}", match.Groups["SizeMb"].Value, line);
                }
            }

            result = default;
            return false;
        }
    }
}
