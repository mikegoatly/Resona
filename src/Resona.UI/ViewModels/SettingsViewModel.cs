using ReactiveUI;

namespace Resona.UI.ViewModels
{
    public class SettingsViewModel : RoutableViewModelBase
    {
        public SettingsViewModel(
            RoutingState router,
            IScreen hostScreen)
            : base(router, hostScreen, "settings")
        {
        }
    }
}
