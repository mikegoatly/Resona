using ReactiveUI;

namespace Resona.UI.ViewModels
{
    public class SettingsViewModel(
        RoutingState router,
        IScreen hostScreen) : RoutableViewModelBase(router, hostScreen, "settings")
    {
    }
}
