using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

using Splat;

namespace Resona.UI.Views.Settings
{
    public partial class BluetoothSettingsView : ReactiveUserControl<BluetoothSettingsViewModel>
    {
        public BluetoothSettingsView()
        {
            this.InitializeComponent();
            this.ViewModel = Locator.Current.GetService<BluetoothSettingsViewModel>();
        }
    }
}
