using Serilog;

namespace Resona.Services.OS
{
    public static class DrmService
    {
        private static readonly ILogger logger = Log.ForContext("StaticClass", "DrmService");

        public static async Task<string> GetDrmCardAsync(CancellationToken cancellationToken = default)
        {
            logger.Debug("Attempting to find DRM card");
            for (var i = 0; i < 2; i++)
            {
                var command = $"readlink /sys/class/drm/card{i}/device/driver";
                var result = await BashExecutor.ExecuteAsync(command, cancellationToken);

                if (result.Contains("vc4-drm", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Information("Found DRM card at /dev/dri/card{Index}", i);
                    return $"/dev/dri/card{i}";
                }

                logger.Debug("No DRM card found at /dev/dri/card{Index} ({Output})", i, result);
            }

            throw new InvalidOperationException("No DRM card found");
        }
    }
}
