using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

using Serilog;

using Splat;

namespace Resona.UI.Views
{
    public partial class MainView : ReactiveUserControl<MainWindowViewModel>
    {
        public MainView()
        {
            Log.Information("Main view starting");

            AvaloniaXamlLoader.Load(this);
            this.ViewModel = Locator.Current.GetService<MainWindowViewModel>();
            this.ViewModel!.GoHome.Execute();
        }
    }
}
