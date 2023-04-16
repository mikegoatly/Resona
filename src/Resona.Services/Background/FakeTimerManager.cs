namespace Resona.Services.Background
{
#if DEBUG
    public class FakeTimerManager : ITimerManager
    {
        public Action? ShowConfiguration { get; set; }
        public Action? SleepTimerCompleted { get; set; }
        public Action<TimeSpan>? SleepTimerUpdated { get; set; }
        public Action<bool>? ScreenDimStateChanged { get; set; }

        public void CancelSleepTimer()
        {
        }

        public void ResetInactivityTimers()
        {
        }

        public void SetSleepTimer(TimeSpan duration)
        {
        }
    }
#endif
}
