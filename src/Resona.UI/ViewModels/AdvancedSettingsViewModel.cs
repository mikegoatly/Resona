using System;
using System.Reactive;
using System.Reactive.Linq;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Persistence;
using Resona.Services;
using Resona.Services.Libraries;
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
            : this(new DevLogService(), new FakeLibrarySyncer())
        {
        }
#endif

        public AdvancedSettingsViewModel(ILogService logService, ILibrarySyncer librarySyncer)
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

            this.RebuildLibraryDataCommand = ReactiveCommand.Create(() =>
            {
                ResonaDb.Reset();
                librarySyncer.StartSync();
            });
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
        public ReactiveCommand<Unit, Unit> RebuildLibraryDataCommand { get; }

        private void RefreshLogSize()
        {
            this.refreshLogSizeSubscription?.Dispose();
            this.refreshLogSizeSubscription = Observable.FromAsync(this.logService.GetLogSizeMbAsync)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => this.LogSize = x);
        }
    }
}
