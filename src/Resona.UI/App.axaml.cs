using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Resona.Services.Libraries;
using Resona.UI.Views;

using Serilog;

using Splat;

namespace Resona.UI
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }
            else if (this.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new MainSingleView();
            }

            // Always sync once the app is ready
            var syncer = Locator.Current.GetService<ILibrarySyncer>();
            if (syncer != null)
            {
                Task.Run(async () =>
                {
                    // Wait for the application to warm up before hitting the storage with a sync process
                    await Task.Delay(3000);
                    syncer.StartSync();
                });
            }
            else
            {
                Log.ForContext<App>().Error("No ILibrarySyncer implementation found");
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
