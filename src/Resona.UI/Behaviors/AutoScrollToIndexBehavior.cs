using System;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace Resona.UI.Behaviors
{
    /// <summary>
    /// A behavior that automatically scrolls an <see cref="ItemsControl"/> within its containing <see cref="ScrollViewer"/> so that an item is as close to the 
    /// middle of the viewport as possible.
    /// </summary>
    public class AutoScrollToIndexBehavior : Behavior<ItemsControl>
    {
        private CancellationTokenSource? animationCancellationTokenSource;
        private IDisposable? propertyChangeSubscription;

        public static readonly AttachedProperty<int> CurrentIndexProperty = AvaloniaProperty.RegisterAttached<AutoScrollToIndexBehavior, ItemsControl, int>("CurrentIndex", 0, false);
        public static readonly AttachedProperty<Easing?> EasingProperty = AvaloniaProperty.RegisterAttached<AutoScrollToIndexBehavior, ItemsControl, Easing?>("Easing", null, false);
        public static readonly AttachedProperty<TimeSpan?> ScrollDurationProperty = AvaloniaProperty.RegisterAttached<AutoScrollToIndexBehavior, ItemsControl, TimeSpan?>("ScrollDuration", null, false);

        public int CurrentIndex
        {
            get => this.GetValue(CurrentIndexProperty);
            set => this.SetValue(CurrentIndexProperty, value);
        }

        public Easing? Easing
        {
            get => this.GetValue(EasingProperty);
            set => this.SetValue(EasingProperty, value);
        }

        public TimeSpan? ScrollDuration
        {
            get => this.GetValue(ScrollDurationProperty);
            set => this.SetValue(ScrollDurationProperty, value);
        }

        protected override void OnDetaching()
        {
            this.propertyChangeSubscription?.Dispose();

            base.OnDetaching();
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (this.AssociatedObject != null)
            {
                this.propertyChangeSubscription = CurrentIndexProperty.Changed.Subscribe(
                    (args) => this.ScrollToIndex(args.GetNewValue<int>(), true));

                // Ensure that the current index that's specified on attach is in view
                // without animating the scroll
                this.ScrollToIndex(this.CurrentIndex, false);
            }
        }

        private async void ScrollToIndex(int index, bool animate)
        {
            if (this.AssociatedObject == null)
            {
                return;
            }

            var scrollViewer = this.AssociatedObject.GetLogicalParent<ScrollViewer>();
            if (scrollViewer == null)
            {
                return;
            }

            // Cancel any existing scroll animation
            this.animationCancellationTokenSource?.Cancel();
            this.animationCancellationTokenSource = new CancellationTokenSource();

            // Wait for the control to be materialized and its position to be updated
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(10);

                if (this.AssociatedObject.ContainerFromIndex(index) is Control container)
                {
                    // Get the position of the container relative to the top of the ItemsControl
                    var point = container.TranslatePoint(new Point(0, 0), this.AssociatedObject);
                    if (point.HasValue)
                    {
                        // Calculate the target offset so that the container is centred in the viewport
                        var targetY = point.GetValueOrDefault().Y - (scrollViewer.Bounds.Height / 2) + (container.Bounds.Height / 2);

                        // Ensure the target offset is within the valid scroll range
                        targetY = Math.Clamp(targetY, 0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

                        var targetOffset = new Vector(0, targetY);
                        if (animate)
                        {
                            // Animate the scroll
                            await scrollViewer.AnimateOffsetAsync(
                                targetOffset,
                                this.ScrollDuration ?? TimeSpan.FromSeconds(1),
                                this.Easing,
                                this.animationCancellationTokenSource.Token);
                        }
                        else
                        {
                            scrollViewer.Offset = targetOffset;
                        }
                    }
                }
            }, DispatcherPriority.Render);
        }
    }
}
