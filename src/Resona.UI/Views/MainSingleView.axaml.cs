using System;

using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia;

namespace Resona.UI.Views
{
    /// <summary>
    /// Hosts <see cref="MainView"/> on single-view platforms: Android, and the Pi when started with --drm.
    /// Keeps the content inside the system bars / display cutouts (no insets manager on DRM, so no padding there)
    /// and scales down below <see cref="DesignHeight"/> (the Pi's 600px height is above it, so it renders at 1:1).
    /// </summary>
    public partial class MainSingleView : UserControl
    {
        /// <summary>
        /// The UI is laid out for a 1024x600 display. A phone in landscape is much shorter than that, so below
        /// this height the whole UI is scaled down (never up) and laid out as though it had this height.
        /// </summary>
        private const double DesignHeight = 480;

        private readonly ScaleTransform scaleTransform = new();
        private TopLevel? topLevel;

        public MainSingleView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty || change.Property == PaddingProperty)
            {
                this.UpdateScale();
            }
        }

        private void UpdateScale()
        {
            if (this.Content is not LayoutTransformControl scaler)
            {
                return;
            }

            var available = this.Bounds.Height - this.Padding.Top - this.Padding.Bottom;
            var scale = available > 0 ? Math.Min(1, available / DesignHeight) : 1;
            if (!ReferenceEquals(scaler.LayoutTransform, scaleTransform))
            {
                scaler.LayoutTransform = scaleTransform;
            }

            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            this.topLevel = TopLevel.GetTopLevel(this);
            if (this.topLevel?.InsetsManager is { } insets)
            {
                insets.SafeAreaChanged += this.OnSafeAreaChanged;
                this.Padding = insets.SafeAreaPadding;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (this.topLevel?.InsetsManager is { } insets)
            {
                insets.SafeAreaChanged -= this.OnSafeAreaChanged;
            }

            this.topLevel = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => this.Padding = e.SafeAreaPadding;
    }
}
