using ReactiveUI;

namespace Resona.UI.ViewModels
{
    public class SettingsViewModel : RoutableViewModelBase
    {
        public SettingsViewModel(
            RoutingState router,
            IScreen hostScreen,
            AudioSettingsViewModel audioSettingsViewModel,
            BluetoothSettingsViewModel bluetoothSettings)
            : base(router, hostScreen, "settings")
        {
            this.AudioSettings = audioSettingsViewModel;
            this.BluetoothSettings = bluetoothSettings;
        }

        public AudioSettingsViewModel AudioSettings { get; }
        public BluetoothSettingsViewModel BluetoothSettings { get; }
    }
}
