using ReactiveUI.Avalonia.Reactive;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class SpotifyTrackListView : ReactiveUserControl<SpotifyTrackListViewModel>
    {
        public SpotifyTrackListView()
        {
            this.InitializeComponent();
        }
    }
}
