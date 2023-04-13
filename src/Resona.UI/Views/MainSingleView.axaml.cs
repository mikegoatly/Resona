using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Resona.UI.Views
{
    public partial class MainSingleView : UserControl
    {
        public MainSingleView()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
