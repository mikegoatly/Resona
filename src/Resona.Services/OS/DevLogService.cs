namespace Resona.Services.OS
{
    public class DevLogService : ILogService
    {
        public Task ClearLogsAsync()
        {
            return Task.CompletedTask;
        }

        public Task<float?> GetLogSizeMbAsync()
        {
            return Task.FromResult<float?>(100F);
        }
    }
}
