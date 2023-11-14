using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Windows.Input;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Background;

namespace Resona.UI.ViewModels
{
    public class SleepOptionsViewModel : ReactiveObject
    {
        private readonly ITimerManager timerManager;

#if DEBUG
        public SleepOptionsViewModel()
            : this(new FakeTimerManager())
        {
            this.SleepModeActive = true;
            this.RemainingSleepTime = "1 hour";
        }
#endif

        public SleepOptionsViewModel(ITimerManager timerManager)
        {
            this.timerManager = timerManager;

            this.SleepCommands = new List<SleepOption>()
            {
                new SleepOption("1 minute", ReactiveCommand.Create(() => this.timerManager.SetSleepTimer(TimeSpan.FromMinutes(1)))),
                new SleepOption("5 minutes", ReactiveCommand.Create(() => this.timerManager.SetSleepTimer(TimeSpan.FromMinutes(5)))),
                new SleepOption("10 minutes", ReactiveCommand.Create(() => this.timerManager.SetSleepTimer(TimeSpan.FromMinutes(10)))),
                new SleepOption("15 minutes", ReactiveCommand.Create(() => this.timerManager.SetSleepTimer(TimeSpan.FromMinutes(15)))),
                new SleepOption("30 minutes", ReactiveCommand.Create(() => this.timerManager.SetSleepTimer(TimeSpan.FromMinutes(30)))),
                new SleepOption("1 hour", ReactiveCommand.Create(() => this.timerManager.SetSleepTimer(TimeSpan.FromHours(1)))),
            };

            timerManager.SleepTimerUpdated
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
            {
                this.SleepModeActive = x != null;
                this.RemainingSleepTime = x switch
                {
                    null => null,
                    TimeSpan t when t < TimeSpan.FromHours(1D) && t.Minutes <= 1D => $"{Math.Ceiling(t.TotalMinutes)} min",
                    TimeSpan t when t < TimeSpan.FromHours(1D) => $"{Math.Ceiling(t.TotalMinutes)} mins",
                    TimeSpan t when t.Minutes <= 0D && t.Hours <= 1D => $"{Math.Ceiling(t.TotalHours)} hour",
                    TimeSpan t => $"{Math.Ceiling(t.TotalHours)} hours",
                };
            });

            this.CancelSleep = ReactiveCommand.Create(
                this.timerManager.CancelSleepTimer,
                this.WhenAnyValue(x => x.SleepModeActive, x => x == true));
        }

        [Reactive]
        public bool SleepModeActive { get; set; }

        [Reactive]
        public string? RemainingSleepTime { get; set; }

        public IReadOnlyList<SleepOption> SleepCommands { get; }

        public ICommand CancelSleep { get; }
    }

    public class SleepOption(string name, ICommand command)
    {
        public string Name { get; } = name;
        public ICommand Command { get; } = command;
    }
}
