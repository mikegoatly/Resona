using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

using Serilog;

namespace Resona.Services.OS
{
    public enum BashCommandKind
    {
        Execute,
        WaitForLineOutputMatch
    }

    public interface IBashCommand
    {
        BashCommandKind Kind { get; }
    }

    public class BashCommand : IBashCommand
    {
        public BashCommandKind Kind => BashCommandKind.Execute;

        public string Command { get; }

        public BashCommand(string command)
        {
            this.Command = command;
        }
    }

    public class WaitForOutput : IBashCommand
    {
        public BashCommandKind Kind => BashCommandKind.Execute;

        public Regex OutputMatcher { get; }

        public WaitForOutput(Regex outputMatcher)
        {
            this.OutputMatcher = outputMatcher;
        }
    }

    internal static class BashExecutor
    {
        private static readonly ILogger logger = Log.ForContext("StaticClass", "BashExecutor");
        public delegate bool TryMatchOutput<TResult>(string line, [NotNullWhen(true)] out TResult? result);

        public static async Task<IReadOnlyList<TResult>> ExecuteAsync<TResult>(
            string command,
            TryMatchOutput<TResult> lineProcessor,
            CancellationToken cancellationToken)
        {
            var output = new List<TResult>();

            await ExecuteBashCommandAsync(
                command,
                (sender, args) =>
                {
                    var line = args.Data;
                    logger.Verbose("Output: {Line}", line);
                    if (line != default && lineProcessor(line, out var match))
                    {
                        logger.Verbose("Matched: {@Match}", match);
                        output.Add(match);
                    }
                },
                cancellationToken);

            return output;
        }

        public static async Task<string> ExecuteAsync(
            string command,
            CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            await ExecuteBashCommandAsync(
                command,
                (sender, args) =>
                {
                    var line = args.Data;
                    if (line == null)
                    {
                        output.AppendLine();
                    }
                    else
                    {
                        output.AppendLine(line);
                    }
                },
                cancellationToken);

            var result = output.ToString();
            logger.Verbose("Output: {Line}", result);

            return result;
        }

        private static async Task ExecuteBashCommandAsync(string command, DataReceivedEventHandler outputReceived, CancellationToken cancellationToken)
        {
            logger.Verbose("Executing: {Command}", command);

            var info = new ProcessStartInfo("bash", $"-c \"{command}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            var p = new Process() { StartInfo = info };

            p.OutputDataReceived += outputReceived;

            p.ErrorDataReceived += (sender, args) =>
            {
                var errorLine = args.Data;
                if (!string.IsNullOrEmpty(errorLine))
                {
                    logger.Error("Error: {ErrorLine}", errorLine);
                }
            };

            p.Start();

            p.BeginOutputReadLine();

            await p.WaitForExitAsync(cancellationToken);

            logger.Verbose("Exit code {ExitCode}", p.ExitCode);
        }
    }
}
