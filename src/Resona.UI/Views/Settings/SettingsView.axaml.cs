using Avalonia.ReactiveUI;

using ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views.Settings
{
    public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
    {
        public SettingsView()
        {
            this.WhenActivated(disposables => { });
            this.InitializeComponent();
        }
    }
}
