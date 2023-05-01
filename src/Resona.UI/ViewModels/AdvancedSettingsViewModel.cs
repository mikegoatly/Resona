using System;
using System.Reactive;
using System.Reactive.Linq;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services;
using Resona.Services.OS;

namespace Resona.UI.ViewModels
{
    public class AdvancedSettingsViewModel : ReactiveObject
    {
        private readonly ILogService logService;
        private IDisposable? refreshLogSizeSubscription;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public AdvancedSettingsViewModel()
            : this(new FakeLogService())
        {
        }
#endif

        public AdvancedSettingsViewModel(ILogService logService)
        {
            this.logService = logService;
            this.RefreshLogSizeCommand = ReactiveCommand.Create(this.RefreshLogSize);
            this.ClearLogsCommand = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    await this.logService.ClearLogsAsync();
                    this.RefreshLogSize();
                });

            this.RefreshLogSize();
        }

        public ReactiveCommand<Unit, Unit> RefreshLogSizeCommand { get; }
        public ReactiveCommand<Unit, Unit> ClearLogsCommand { get; }

        public string[] LogLevelOptions { get; } = new[] { "Verbose", "Debug", "Information", "Warning", "Error" };

        public string LogLevel
        {
            get => Settings.Default.LogLevel;
            set
            {
                Settings.Default.LogLevel = value;
                Settings.Default.Save();
                this.RaisePropertyChanged();
            }
        }

        [Reactive]
        public float? LogSize { get; set; }

        private void RefreshLogSize()
        {
            this.refreshLogSizeSubscription?.Dispose();
            this.refreshLogSizeSubscription = Observable.FromAsync(this.logService.GetLogSizeMbAsync)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => this.LogSize = x);
        }
    }
}
