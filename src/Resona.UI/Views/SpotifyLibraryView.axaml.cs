using Avalonia.Markup.Xaml;

using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class SpotifyLibraryView : ReactiveUserControl<SpotifyLibraryViewModel>
    {
        public SpotifyLibraryView()
        {
            this.WhenActivated((System.Action<System.IDisposable> _) => { });
            AvaloniaXamlLoader.Load(this);
        }
    }
}
