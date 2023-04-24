using ReactiveUI;

namespace Resona.UI.ViewModels
{
    public class SettingsViewModel : RoutableViewModelBase
    {
        public SettingsViewModel(
            RoutingState router,
            IScreen hostScreen,
            BluetoothSettingsViewModel bluetoothSettings)
            : base(router, hostScreen, "settings")
        {
            this.BluetoothSettings = bluetoothSettings;
        }

        public BluetoothSettingsViewModel BluetoothSettings { get; }
    }
}
