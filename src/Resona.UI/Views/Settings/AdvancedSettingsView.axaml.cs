using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

using Splat;

namespace Resona.UI.Views.Settings
{
    public partial class AdvancedSettingsView : ReactiveUserControl<AdvancedSettingsViewModel>
    {
        public AdvancedSettingsView()
        {
            this.InitializeComponent();
            this.ViewModel = Locator.Current.GetService<AdvancedSettingsViewModel>();
        }
    }
}
