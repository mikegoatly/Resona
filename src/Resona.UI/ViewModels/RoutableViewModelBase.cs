using ReactiveUI;

namespace Resona.UI.ViewModels
{
    public abstract class RoutableViewModelBase : ReactiveObject, IRoutableViewModel
    {
        public RoutableViewModelBase(RoutingState router, IScreen hostScreen, string urlPathSegment)
        {
            Router = router;
            HostScreen = hostScreen;
            UrlPathSegment = urlPathSegment;
        }

        public string? UrlPathSegment { get; }
        public RoutingState Router { get; }
        public IScreen HostScreen { get; }
    }
}
