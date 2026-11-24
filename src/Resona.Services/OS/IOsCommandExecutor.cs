namespace Resona.Services.OS
{
    public interface IOsCommandExecutor
    {
        void Restart();
        void Shutdown();
    }
}
