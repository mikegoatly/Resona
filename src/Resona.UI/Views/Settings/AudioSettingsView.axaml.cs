using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

using Splat;

namespace Resona.UI.Views.Settings
{
    public partial class AudioSettingsView : ReactiveUserControl<AudioSettingsViewModel>
    {
        public AudioSettingsView()
        {
            this.InitializeComponent();
            this.ViewModel = Locator.Current.GetService<AudioSettingsViewModel>();
        }
    }
}
