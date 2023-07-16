using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class AudioSelectionView : ReactiveUserControl<AudioSelectionViewModel>
    {
        public AudioSelectionView()
        {
            this.WhenActivated(disposables => { });
            AvaloniaXamlLoader.Load(this);
        }
    }
}
