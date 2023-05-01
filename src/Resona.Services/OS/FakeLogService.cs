namespace Resona.Services.OS
{
#if DEBUG
    public class FakeLogService : ILogService
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
#endif
}
