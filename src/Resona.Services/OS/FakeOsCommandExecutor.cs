namespace Resona.Services.OS
{
#if DEBUG
    public class FakeOsCommandExecutor : IOsCommandExecutor
    {
        public void Restart()
        {
        }

        public void Shutdown()
        {
        }
    }
#endif
}
