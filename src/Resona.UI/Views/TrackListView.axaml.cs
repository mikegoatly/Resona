using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class TrackListView : ReactiveUserControl<TrackListViewModel>
    {
        public TrackListView()
        {
            InitializeComponent();
        }
    }
}
