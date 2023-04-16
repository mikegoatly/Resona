using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

using Splat;

namespace Resona.UI.Views
{
    public partial class PowerOptions : ReactiveUserControl<PowerOptionsViewModel>
    {
        public PowerOptions()
        {
            this.InitializeComponent();

            this.ViewModel = Locator.Current.GetService<PowerOptionsViewModel>();
        }
    }
}
