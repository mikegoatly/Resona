namespace Resona.Services.Background
{
    public class SleepConfiguration
    {
        public required TimeSpan InactivityShutdownTimeout { get; set; }
        public required TimeSpan ScreenDimTimeout { get; set; }
    }
}
