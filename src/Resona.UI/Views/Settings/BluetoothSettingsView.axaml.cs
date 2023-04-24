using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views.Settings
{
    public partial class BluetoothSettingsView : ReactiveUserControl<BluetoothSettingsViewModel>
    {
        public BluetoothSettingsView()
        {
            this.InitializeComponent();
        }
    }
}
