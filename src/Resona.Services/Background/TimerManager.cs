using Microsoft.Extensions.Options;

using Resona.Services.OS;

using Serilog;

namespace Resona.Services.Background
{
    public interface ITimerManager
    {
        Action? ShowConfiguration { get; set; }
        Action? SleepTimerCompleted { get; set; }
        Action<TimeSpan>? SleepTimerUpdated { get; set; }
        Action<bool>? ScreenDimStateChanged { get; set; }

        void CancelSleepTimer();
        void ResetInactivityTimers();
        void SetSleepTimer(TimeSpan duration);
    }

    internal class TimerManager : ITimerManager
    {
        private static readonly ILogger logger = Log.ForContext<TimerManager>();

        private readonly SleepConfiguration configuration;

        /// <summary>
        /// We have one timer that ticks every minute, and we use it to check if we need to perform any timed actions
        /// </summary>
        private readonly Timer timer;

        /// <summary>
        /// And another timer for dimming the screen. This is separate because screen dimming is often at a more granular time cadence, i.e. sub-minute.
        /// </summary>
        private readonly Timer screenDimTimer;

        private readonly IOsCommandExecutor osCommandExecutor;

        /// <summary>
        /// The date and time at which playback should automatically be stopped.
        /// </summary>
        private DateTime sleepTime;

        /// <summary>
        /// The date and time at which the system should shut down.
        /// </summary>
        private DateTime shutDownTime;

        /// <summary>
        /// Whether the user has set a sleep timer.
        /// </summary>
        private bool sleepTimerActive;
        private bool screenDimmed;

        public TimerManager(IOptions<SleepConfiguration> sleepConfiguration, IOsCommandExecutor osCommandExecutor)
        {
            this.configuration = sleepConfiguration.Value;
            this.timer = new Timer(this.TimerTick);
            this.screenDimTimer = new Timer(this.ScreenDimTimerTick);
            this.ResetInactivityTimers();
            this.RestartTimer();
            this.osCommandExecutor = osCommandExecutor;
        }

        private void ScreenDimTimerTick(object? state)
        {
            this.ChangeScreenDimState(true);
        }

        private void RestartTimer()
        {
            this.timer.Change(
                // Adding an initial delay of 1 second ensures we don't fire a little early and miss a minute's change
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMinutes(1));
        }

        public Action? ShowConfiguration { get; set; }

        public Action<TimeSpan>? SleepTimerUpdated { get; set; }

        public Action? SleepTimerCompleted { get; set; }

        public Action<bool>? ScreenDimStateChanged { get; set; }

        public void ResetInactivityTimers()
        {
            var now = DateTime.UtcNow;
            this.shutDownTime = now.Add(this.configuration.InactivityShutdownTimeout);

            this.screenDimTimer.Change(this.configuration.ScreenDimTimeout, Timeout.InfiniteTimeSpan);

            this.ChangeScreenDimState(false);
        }

        public void SetSleepTimer(TimeSpan duration)
        {
            this.sleepTime = DateTime.UtcNow.Add(duration);

            this.sleepTimerActive = true;

            // Restart the timer so we are ticking at minute intervals relative to now.
            this.RestartTimer();
        }

        public void CancelSleepTimer()
        {
            Console.WriteLine("Sleep timer cancelled");
            this.OnSleepTimerCompleted();
        }

        private void TimerTick(object? state)
        {
            var now = DateTime.UtcNow;

            if (this.sleepTimerActive)
            {
                if (now > this.sleepTime)
                {
                    this.OnSleepTimerCompleted();
                }
                else
                {
                    logger.Debug("Sleep timer ticked");
                    this.SleepTimerUpdated?.Invoke(this.sleepTime - now);
                }
            }

            if (now > this.shutDownTime)
            {
                this.osCommandExecutor.Shutdown();
            }
        }

        private void ChangeScreenDimState(bool dimmed)
        {
            if (this.screenDimmed != dimmed)
            {
                logger.Information("Changing screen dim state to {State}", dimmed);

                this.screenDimmed = dimmed;
                this.ScreenDimStateChanged?.Invoke(dimmed);
            }
        }

        private void OnSleepTimerCompleted()
        {
            logger.Information("Sleep timer ended");

            this.sleepTimerActive = false;
            this.SleepTimerCompleted?.Invoke();
        }
    }
}
