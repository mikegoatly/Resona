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
            throw new NotImplementedException();
        }

        public void ResetInactivityTimers()
        {
            throw new NotImplementedException();
        }

        public void SetSleepTimer(TimeSpan duration)
        {
            throw new NotImplementedException();
        }

        public Task ShutDown()
        {
            throw new NotImplementedException();
        }
    }
#endif
}
