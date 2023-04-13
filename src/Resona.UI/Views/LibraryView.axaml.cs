using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class LibraryView : ReactiveUserControl<LibraryViewModel>
    {
        //private List<(StackPanel visual, ScaleTransform transform)> visibleItems = new();

        public LibraryView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
        }
    }
}
