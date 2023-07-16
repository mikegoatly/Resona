using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class LibraryView : ReactiveUserControl<LibraryViewModel>
    {
        public LibraryView()
        {
            this.WhenActivated(disposables => { });
            AvaloniaXamlLoader.Load(this);
        }
    }
}
