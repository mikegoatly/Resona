using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using ReactiveUI;

using Resona.UI.Behaviors;
using Resona.UI.ViewModels;

namespace Resona.UI.Views
{
    public partial class LibraryView : ReactiveUserControl<LibraryViewModel>
    {
        private readonly ScrollViewer libraryScrollViewer;
        private bool respondingToJumpListPress;
        private ulong pointerPressTime;

        public LibraryView()
        {
            this.WhenActivated(disposables => { });
            AvaloniaXamlLoader.Load(this);
            var scrollViewer = this.FindControl<ScrollViewer>("ScrollViewer");
            if (scrollViewer == null)
            {
                throw new InvalidOperationException("No scroll viewer found!");
            }

            this.libraryScrollViewer = scrollViewer;
        }

        private void ScrollViewer_ScrollChanged(object? sender, Avalonia.Controls.ScrollChangedEventArgs e)
        {
            if (this.respondingToJumpListPress)
            {
                return;
            }

            // Kind of hacky, but given we know: 
            // * 3 items are visible on a row
            // * each item is 245px high
            // We can calculate the index of the first visible item
            if (sender is ScrollViewer scrollViewer)
            {
                var index = (int)scrollViewer.Offset.Y / 245 * 3;

                // Find the first visible item
                if (this.ViewModel?.AudioContent?.Status == TaskStatus.RanToCompletion)
                {
                    var audioContent = this.ViewModel.AudioContent.Result;
                    var item = index < audioContent.Count ? audioContent[index] : null;

                    var lastQuickJumpEntry = this.ViewModel.QuickJumpList[^1];
                    this.ViewModel.CurrentQuickJump = item is not null
                        ? this.ViewModel.QuickJumpList.FirstOrDefault(q => q.Match >= (item.Name.Length == 0 ? '\0' : item.Name[0]), lastQuickJumpEntry)
                        : lastQuickJumpEntry;
                }
            }
        }

        private async void TextBlock_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (e.Timestamp - this.pointerPressTime > 50)
            {
                return;
            }

            if (sender is TextBlock text && text.DataContext is QuickJumpViewModel quickJump && quickJump.IsAvailable)
            {
                // Find the first item that starts with the letter, and scroll to its position
                if (this.ViewModel?.AudioContent?.Status == TaskStatus.RanToCompletion)
                {
                    this.ViewModel.CurrentQuickJump = quickJump;

                    var audioContent = this.ViewModel.AudioContent.Result;
                    var index = audioContent.FindIndex(x => x.Name.Length > 0 && x.Name[0] >= quickJump.Match);

                    if (index >= 0)
                    {
                        this.respondingToJumpListPress = true;
                        await this.libraryScrollViewer.AnimateOffsetAsync(new Point(0, index / 3 * 245), TimeSpan.FromMilliseconds(300), new CircularEaseOut());
                        this.respondingToJumpListPress = false;
                    }
                }
            }
        }

        private void TextBlock_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            this.pointerPressTime = e.Timestamp;
        }

        private async void ItemsControl_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (e.Source is TextBlock text && text.DataContext is QuickJumpViewModel quickJump && quickJump.IsAvailable)
            {
                // Find the first item that starts with the letter, and scroll to its position
                if (this.ViewModel?.AudioContent?.Status == TaskStatus.RanToCompletion)
                {
                    this.ViewModel.CurrentQuickJump = quickJump;

                    var audioContent = this.ViewModel.AudioContent.Result;
                    var index = audioContent.FindIndex(x => x.Name.Length > 0 && x.Name[0] >= quickJump.Match);

                    if (index >= 0)
                    {
                        this.respondingToJumpListPress = true;
                        await this.libraryScrollViewer.AnimateOffsetAsync(new Point(0, index / 3 * 245), TimeSpan.FromMilliseconds(300), new CircularEaseOut());
                        this.respondingToJumpListPress = false;
                    }
                }
            }
        }
    }
}
