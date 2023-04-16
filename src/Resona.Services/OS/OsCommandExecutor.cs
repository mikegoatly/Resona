using System.Diagnostics;

using Serilog;

namespace Resona.Services.OS
{
    public interface IOsCommandExecutor
    {
        void Restart();
        void Shutdown();
    }

    public class OsCommandExecutor : IOsCommandExecutor
    {
        public static ILogger logger = Log.ForContext<OsCommandExecutor>();

        public void Restart()
        {
            // Because we're using systemd with auto restart, we can just kill this process and let systemd restart it
            logger.Information("Restarting the system...");
            Process.GetCurrentProcess().Kill();
        }

        public void Shutdown()
        {
            if (OperatingSystem.IsLinux())
            {
                logger.Information("Shutting down the system...");

                var info = new ProcessStartInfo("bash")
                {
                    RedirectStandardInput = true,
                    UseShellExecute = false
                };

                var p = new Process
                {
                    StartInfo = info
                };

                p.Start();

                using var sw = p.StandardInput;

                // We do two things here:
                // 1. Turn off the USB ports to turn off the screen immediately and hides the OS shutdown text
                // 2. Shutdown the system
                sw.WriteLine("sudo uhubctl -l 2 -a 0 && sudo shutdown now");
            }
        }
    }
}
