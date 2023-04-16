using System;
using System.Reactive;
using System.Reactive.Linq;

using ReactiveUI;

using Resona.Services.Background;

using Serilog;

using Splat;

namespace Resona.UI.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IScreen
    {
        private static readonly Serilog.ILogger logger = Log.ForContext<MainWindowViewModel>();
        private readonly ITimerManager timerManager;
        private double screenDimPercentage = 0D;

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

            Observable.FromEvent<bool>(
                x => this.timerManager.ScreenDimStateChanged += x,
                x => this.timerManager.ScreenDimStateChanged -= x)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => this.ScreenDimPercentage = x ? 0.8D : 0D);
        }

        public RoutingState Router { get; }

        public ReactiveCommand<Unit, Unit> ScreenInteraction { get; }

        public ReactiveCommand<Unit, IRoutableViewModel> GoHome { get; }

        // The command that navigates a user back.
        public ReactiveCommand<Unit, IRoutableViewModel?> GoBack => this.Router.NavigateBack;

        public double ScreenDimPercentage
        {
            get => this.screenDimPercentage;
            set => this.RaiseAndSetIfChanged(ref this.screenDimPercentage, value);
        }
    }
}
