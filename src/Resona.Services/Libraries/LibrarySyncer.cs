using Serilog;

namespace Resona.Services.Libraries
{
    public class LibrarySyncer
    {
        private static readonly ILogger logger = Log.ForContext<LibrarySyncer>();
        private readonly SemaphoreSlim syncLock = new(1);

        public void StartSync()
        {
            if (this.syncLock.Wait(TimeSpan.FromMilliseconds(1)))
            {
                try
                {

                }
                finally
                {
                    this.syncLock.Release();
                }
            }
            else
            {

            }
        }
    }
}
