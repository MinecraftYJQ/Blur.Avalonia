using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Blur.Avalonia
{
    public class BlurSurface : ContentControl
    {
        public static readonly StyledProperty<double> BlurRadiusProperty =
            AvaloniaProperty.Register<BlurSurface, double>(nameof(BlurRadius), 20.0);

        public double BlurRadius
        {
            get => GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }

        static BlurSurface()
        {
            AffectsRender<BlurSurface>(BlurRadiusProperty);
        }

        public override void Render(DrawingContext context)
        {
            var visualRoot = this.GetVisualRoot() as TopLevel;
            if (visualRoot == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

            double scaling = visualRoot.RenderScaling;
            var transformToRoot = this.TransformToVisual(visualRoot);
            if (!transformToRoot.HasValue) return;
            
            var scenePos = new Point(0, 0) * transformToRoot.Value;
            var pixelRect = new Rect(scenePos.X * scaling, scenePos.Y * scaling, 
                                     Bounds.Width * scaling, Bounds.Height * scaling);

            // 提交自定义绘制
            context.Custom(new BlurDrawOperation(
                new Rect(0, 0, Bounds.Width, Bounds.Height), 
                pixelRect, 
                BlurRadius, 
                scaling, 
                visualRoot));

            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
        }
    }

    internal class BlurDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _controlBounds;
        private readonly Rect _pixelRect;
        private readonly double _blurRadius;
        private readonly double _scaling;
        private readonly TopLevel _root;

        public BlurDrawOperation(Rect controlBounds, Rect pixelRect, double blur, double scaling, TopLevel root)
        {
            _controlBounds = controlBounds;
            _pixelRect = pixelRect;
            _blurRadius = blur;
            _scaling = scaling;
            _root = root;
        }

        public void Dispose() { }
        public Rect Bounds => _controlBounds;
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (SharedBackdropManager.IsCapturing) return;

            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (lease == null) return;

            using var skiaLease = lease.Lease();
            var surface = skiaLease.SkSurface;
            if (surface == null) return;

            var screenImage = SharedBackdropManager.GetSnapshot(_root, surface);
            if (screenImage == null) return;

            var canvas = skiaLease.SkCanvas;

            float sigma = (float)(_blurRadius * _scaling / 2.0);
            if (sigma <= 0) sigma = 0.01f;

            float padding = sigma * 2.5f; 
            var expandedSrcRect = new SKRect(
                (float)_pixelRect.Left - padding,
                (float)_pixelRect.Top - padding,
                (float)_pixelRect.Right + padding,
                (float)_pixelRect.Bottom + padding);

            using var blurFilter = SKImageFilter.CreateBlur(
                sigma, 
                sigma, 
                SKShaderTileMode.Clamp); 

            using var paint = new SKPaint
            {
                ImageFilter = blurFilter,
                IsAntialias = true,
                FilterQuality = SKFilterQuality.Medium
            };

            float logicPadding = (float)(padding / _scaling);
            var expandedDestRect = _controlBounds.ToSKRect();
            expandedDestRect.Inflate(logicPadding, logicPadding);

            canvas.Save();
            
            canvas.ClipRect(_controlBounds.ToSKRect());
            
            canvas.DrawImage(screenImage, expandedSrcRect, expandedDestRect, paint);
            
            canvas.Restore();
        }
    }
}