using ReactiveUI;

namespace Resona.UI.ViewModels
{
    public abstract class RoutableViewModelBase : ReactiveObject, IRoutableViewModel
    {
        public RoutableViewModelBase(RoutingState router, IScreen hostScreen, string urlPathSegment)
        {
            this.Router = router;
            this.HostScreen = hostScreen;
            this.UrlPathSegment = urlPathSegment;
        }

        public string? UrlPathSegment { get; }
        public RoutingState Router { get; }
        public IScreen HostScreen { get; }
    }
}
