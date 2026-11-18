using ReactiveUI.Avalonia.Reactive;

using ReactiveUI;
using ReactiveUI.Reactive;

using Resona.UI.ViewModels;

using Splat;

namespace Resona.UI.Views
{
    public partial class BatteryView : ReactiveUserControl<BatteryViewModel>
    {
        public BatteryView()
        {
            this.WhenActivated((System.Action<System.IDisposable> _) => { });
            this.InitializeComponent();
            this.ViewModel = Locator.Current.GetService<BatteryViewModel>();
        }
    }
}
