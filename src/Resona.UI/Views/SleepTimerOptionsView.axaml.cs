using Avalonia.ReactiveUI;

using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class SleepTimerOptionsView : ReactiveUserControl<SleepOptionsViewModel>
    {
        public SleepTimerOptionsView()
        {
            this.InitializeComponent();
        }
    }
}
