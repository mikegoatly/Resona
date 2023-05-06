using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class LibraryView : ReactiveUserControl<LibraryViewModel>
    {
        public LibraryView()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
