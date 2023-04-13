using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

using Splat;

namespace Resona.UI.Views
{
    public partial class PlayerControlsView : ReactiveUserControl<PlayerControlsViewModel>
    {
        public PlayerControlsView()
        {
            InitializeComponent();
            ViewModel = Locator.Current.GetService<PlayerControlsViewModel>();
        }
    }
}
