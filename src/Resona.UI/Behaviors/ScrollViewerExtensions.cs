using System;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Resona.UI.Behaviors
{
    public static class ScrollViewerExtensions
    {
        public static async Task AnimateOffsetAsync(this ScrollViewer scrollViewer, Vector targetOffset, TimeSpan duration, Easing? easing = null, CancellationToken cancellationToken = default)
        {
            var animation = new Animation
            {
                Duration = duration,
                Easing = easing ?? new LinearEasing(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue= new Cue(0D),
                        Setters =
                        {
                            new Setter(ScrollViewer.OffsetProperty, scrollViewer.Offset)
                        }
                    },
                    new KeyFrame
                    {
                        Cue= new Cue(1D),
                        Setters =
                        {
                            new Setter(ScrollViewer.OffsetProperty, targetOffset)
                        }
                    }
                }
            };

            await animation.RunAsync(scrollViewer, cancellationToken: cancellationToken);
        }
    }
}
