using System;
using System.Reactive;

using ReactiveUI;

using Serilog;

using Splat;

namespace Resona.UI.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IScreen
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<MainWindowViewModel>();

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public MainWindowViewModel()
            : this(null!)
        {
        }
#endif

        public MainWindowViewModel(RoutingState router)
        {
            _logger.Debug("Constructing view model");

            Router = router;

            GoHome = ReactiveCommand.CreateFromObservable(
                () =>
                {
                    _logger.Debug("Navigating home");

                    var viewModel = Locator.Current.GetRequiredService<AudioSelectionViewModel>();
                    return Router.Navigate.Execute(viewModel);
                }
            );
        }

        public RoutingState Router { get; }


        public ReactiveCommand<Unit, IRoutableViewModel> GoHome { get; }


        // The command that navigates a user back.
        public ReactiveCommand<Unit, IRoutableViewModel?> GoBack => Router.NavigateBack;
    }
}
