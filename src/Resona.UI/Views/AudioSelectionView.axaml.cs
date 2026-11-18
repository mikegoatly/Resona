using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia.Reactive;

using ReactiveUI;
using ReactiveUI.Reactive;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class AudioSelectionView : ReactiveUserControl<AudioSelectionViewModel>
    {
        public AudioSelectionView()
        {
            this.WhenActivated((System.Action<System.IDisposable> _) => { });
            AvaloniaXamlLoader.Load(this);
        }
    }
}
