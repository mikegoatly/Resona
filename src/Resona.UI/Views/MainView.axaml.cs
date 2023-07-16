using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using ReactiveUI;

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

            this.WhenActivated(disposables => { });

            AvaloniaXamlLoader.Load(this);
            this.ViewModel = Locator.Current.GetService<MainWindowViewModel>();
            this.ViewModel?.GoHome.Execute();
        }
    }
}
