using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;

using Resona.UI.ViewModels;

namespace Resona.UI.Views.Settings
{
    public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
    {
        public SettingsView()
        {
            this.WhenActivated((System.Action<System.IDisposable> _) => { });
            this.InitializeComponent();
        }
    }
}
