using System;

using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia.Reactive;

using ReactiveUI;
using ReactiveUI.Reactive;

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

            this.WhenActivated((System.Action<System.IDisposable> _) => { });

            AvaloniaXamlLoader.Load(this);
            this.ViewModel = Locator.Current.GetService<MainWindowViewModel>();
            this.ViewModel?.GoHome.Execute().Subscribe();
        }
    }
}
