using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views.Settings
{
    public partial class AudioSettingsView : ReactiveUserControl<AudioSettingsViewModel>
    {
        public AudioSettingsView()
        {
            this.InitializeComponent();
        }
    }
}
