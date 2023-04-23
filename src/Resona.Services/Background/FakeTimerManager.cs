using System.Reactive.Linq;

namespace Resona.Services.Background
{
#if DEBUG
    public class FakeTimerManager : ITimerManager
    {
        public Action? SleepTimerCompleted { get; set; }

        IObservable<TimeSpan?> ITimerManager.SleepTimerUpdated => Observable.Empty<TimeSpan?>();

        IObservable<bool> ITimerManager.ScreenDimStateChanged => Observable.Empty<bool>();

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
