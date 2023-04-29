using System;
using System.Reactive;
using System.Reactive.Linq;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Background;

using Serilog;

using Splat;

namespace Resona.UI.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IScreen
    {
        private static readonly Serilog.ILogger logger = Log.ForContext<MainWindowViewModel>();
        private readonly ITimerManager timerManager;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public MainWindowViewModel()
            : this(null!, new FakeTimerManager())
        {
        }
#endif

        public MainWindowViewModel(RoutingState router, ITimerManager timerManager)
        {
            logger.Debug("Constructing view model");

            this.Router = router;
            this.timerManager = timerManager;
            this.GoHome = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    logger.Debug("Navigating home");

                    var viewModel = Locator.Current.GetRequiredService<AudioSelectionViewModel>();
                    return this.Router.Navigate.Execute(viewModel);
                }
            );

            this.ScreenInteraction = ReactiveCommand.Create(this.timerManager.ResetInactivityTimers);

            this.timerManager.ScreenDimStateChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => this.ScreenDimPercentage = x ? 0.8D : 0D);

            this.NavigateToSettingsCommand = ReactiveCommand.Create(() => this.Router.Navigate.Execute(Locator.Current.GetService<SettingsViewModel>()!));

            this.Router.NavigateBack.CanExecute
                .Throttle(TimeSpan.FromMilliseconds(100))
                .DistinctUntilChanged()
                .Subscribe(x => this.CanGoBack = x == true);

            this.Router.CurrentViewModel
                .Where(x => x != null)
                .Select(x => x!.GetType())
                .DistinctUntilChanged()
                .Subscribe(x => this.ShowSettingsButton = x != typeof(SettingsViewModel));

        }

        public RoutingState Router { get; }

        public ReactiveCommand<Unit, Unit> ScreenInteraction { get; }

        public ReactiveCommand<Unit, IRoutableViewModel> GoHome { get; }

        // The command that navigates a user back.
        public ReactiveCommand<Unit, IRoutableViewModel?> GoBack => this.Router.NavigateBack;

        [Reactive]
        public double ScreenDimPercentage { get; set; }

        [Reactive]
        public bool CanGoBack { get; set; }

        [Reactive]
        public bool ShowSettingsButton { get; set; }

        public ReactiveCommand<Unit, IObservable<IRoutableViewModel>> NavigateToSettingsCommand { get; }
    }
}
