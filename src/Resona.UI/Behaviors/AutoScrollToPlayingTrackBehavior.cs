using System;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

using Resona.UI.ViewModels;

namespace Resona.UI.Behaviors
{
    public class AutoScrollToPlayingBehavior : Behavior<ItemsControl>
    {
        protected override void OnDetaching()
        {
            if (this.AssociatedObject != null)
            {
                this.AssociatedObject.DataContextChanged -= this.OnDataContextChanged;
            }

            base.OnDetaching();
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (this.AssociatedObject != null)
            {
                this.AssociatedObject.DataContextChanged += this.OnDataContextChanged;

                // Immediately scroll to the playing item to pick up the current state
                this.ScrollToPlayingItem();
            }
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (this.AssociatedObject?.DataContext is TrackListViewModel viewModel)
            {
                viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TrackListViewModel.CurrentTrack))
                    {
                        this.ScrollToPlayingItem();
                    }
                };
            }
        }

        private CancellationTokenSource? cancellationTokenSource;

        private async void ScrollToPlayingItem()
        {
            if (this.AssociatedObject?.DataContext is TrackListViewModel viewModel && viewModel.CurrentTrack != null)
            {
                var scrollViewer = this.AssociatedObject.GetLogicalParent<ScrollViewer>();
                if (scrollViewer == null)
                {
                    return;
                }

                // Cancel any existing scroll animation
                this.cancellationTokenSource?.Cancel();
                this.cancellationTokenSource = new CancellationTokenSource();

                if (this.AssociatedObject.ContainerFromIndex(viewModel.CurrentTrack.Index) is Control container)
                {
                    // Wait for the control to be materialized and its position to be updated
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await Task.Delay(10);

                        var point = container.TranslatePoint(new Point(0, 0), this.AssociatedObject);
                        if (point.HasValue)
                        {
                            var targetY = point.Value.Y - (scrollViewer.Bounds.Height / 2) + (container.Bounds.Height / 2);
                            var targetOffset = new Vector(0, targetY);

                            // Animate the scroll
                            await scrollViewer.AnimateOffsetAsync(
                                targetOffset,
                                TimeSpan.FromMilliseconds(300),
                                new SineEaseInOut(),
                                this.cancellationTokenSource.Token);
                        }
                    }, DispatcherPriority.Layout);
                }
            }
        }
    }
}
