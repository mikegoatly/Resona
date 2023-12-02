using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Resona.UI.ViewModels
{
    public class QuickJumpViewModel : ReactiveObject
    {
        private static int index = 0;

        public QuickJumpViewModel(string display, char match)
        {
            this.Display = display;
            this.Match = match;
            this.Index = index++;
        }
        public QuickJumpViewModel(char match)
            : this(new string(match, 1), match)
        {
        }

        public string Display { get; }
        public char Match { get; }
        public int Index { get; }

        [Reactive]
        public bool IsCurrent { get; set; }

        [Reactive]
        public bool IsAvailable { get; set; }
    }
}
