using System.Diagnostics;

using Serilog;

namespace Resona.Services.OS
{
    /// <summary>
    /// Development (non-Linux desktop) behaviour: "shutting down" or "restarting" just ends the process.
    /// </summary>
    public class DevOsCommandExecutor : IOsCommandExecutor
    {
        private static readonly ILogger logger = Log.ForContext<DevOsCommandExecutor>();

        public void Restart()
        {
            logger.Information("Restarting the system...");
            Process.GetCurrentProcess().Kill();
        }

        public void Shutdown()
        {
            Process.GetCurrentProcess().Kill();
        }
    }
}
