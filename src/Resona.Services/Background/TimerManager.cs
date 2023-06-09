﻿using System.Reactive.Subjects;

using Resona.Services.OS;

using Serilog;

namespace Resona.Services.Background
{
    public interface ITimerManager
    {
        IObservable<TimeSpan?> SleepTimerUpdated { get; }
        Action? SleepTimerCompleted { get; set; }
        IObservable<bool> ScreenDimStateChanged { get; }

        void CancelSleepTimer();
        void ResetInactivityTimers();
        void SetSleepTimer(TimeSpan duration);
    }

    internal class TimerManager : ITimerManager
    {
        private static readonly ILogger logger = Log.ForContext<TimerManager>();

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

        private readonly Subject<bool> screenDimStateChanged = new();
        private readonly Subject<TimeSpan?> sleepTimerUpdated = new();

        public TimerManager(IOsCommandExecutor osCommandExecutor)
        {
            logger.Debug("Timer manager starting");

            this.timer = new Timer(this.TimerTick);
            this.screenDimTimer = new Timer(this.ScreenDimTimerTick);
            this.ResetInactivityTimers();
            this.RestartTimer();
            this.osCommandExecutor = osCommandExecutor;

            Settings.Default.SettingsChanged += this.SettingsChanged;
        }

        private void ScreenDimTimerTick(object? state)
        {
            this.ChangeScreenDimState(true);
        }

        private void RestartTimer()
        {
            logger.Debug("Restarting timer");

            this.timer.Change(
                TimeSpan.FromSeconds(0.1),
                TimeSpan.FromMinutes(1));
        }

        public IObservable<TimeSpan?> SleepTimerUpdated => this.sleepTimerUpdated;

        public IObservable<bool> ScreenDimStateChanged => this.screenDimStateChanged;

        public Action? SleepTimerCompleted { get; set; }

        public void ResetInactivityTimers()
        {
            var now = DateTime.UtcNow;
            this.shutDownTime = now.Add(Settings.Default.InactivityShutdownTimeout);

            logger.Debug("Shut down time is now {ShutDownTime}", this.shutDownTime);

            this.screenDimTimer.Change(Settings.Default.ScreenDimTimeout, Timeout.InfiniteTimeSpan);

            this.ChangeScreenDimState(false);
        }

        public void SetSleepTimer(TimeSpan duration)
        {
            logger.Information("Setting sleep timer to {Duration}", duration);

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

        private void SettingsChanged()
        {
            logger.Debug("Detected settings changed, resetting timers");
            this.ResetInactivityTimers();
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
                    this.sleepTimerUpdated.OnNext(this.sleepTime - now);
                }
            }

            if (now > this.shutDownTime.AddMinutes(30))
            {
                // Sometimes the app seems to resume without the process being started. After a clean Pi reboot,
                // the journal logs just continue from where they left off. I have no clue.
                // To mitigate this, we detect a massive overrun in the timer and reset them.
                logger.Warning("Current time {Now} greatly exceeds shutdown time {ShutDownTime} - assuming system has resumed. Resetting sleep timer.", now, this.shutDownTime);

                this.ResetInactivityTimers();
            }
            else if (now > this.shutDownTime)
            {
                logger.Information("Current time {Now} exceeded shutdown time {ShutDownTime}", now, this.shutDownTime);

                this.osCommandExecutor.Shutdown();
            }
        }

        private void ChangeScreenDimState(bool dimmed)
        {
            if (this.screenDimmed != dimmed)
            {
                logger.Information("Changing screen dim state to {State}", dimmed);

                this.screenDimmed = dimmed;
                this.screenDimStateChanged.OnNext(dimmed);
            }
        }

        private void OnSleepTimerCompleted()
        {
            logger.Information("Sleep timer ended");

            this.sleepTimerActive = false;
            this.sleepTimerUpdated.OnNext(null);
            this.SleepTimerCompleted?.Invoke();
        }
    }
}
