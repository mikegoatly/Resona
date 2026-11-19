using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace Resona.UI.ViewModels
{
    public partial class QuickJumpViewModel : ReactiveObject
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
        public partial bool IsCurrent { get; set; }

        [Reactive]
        public partial bool IsAvailable { get; set; }
    }
}
