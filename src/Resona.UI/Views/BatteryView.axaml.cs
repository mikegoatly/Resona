using Avalonia.ReactiveUI;

using ReactiveUI;

using Resona.UI.ViewModels;

using Splat;

namespace Resona.UI.Views
{
    public partial class BatteryView : ReactiveUserControl<BatteryViewModel>
    {
        public BatteryView()
        {
            this.WhenActivated(disposables => { });
            this.InitializeComponent();
            this.ViewModel = Locator.Current.GetService<BatteryViewModel>();
        }
    }
}
