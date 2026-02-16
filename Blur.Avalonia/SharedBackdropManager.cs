using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Blur.Avalonia
{
    internal static class SharedBackdropManager
    {
        public static bool IsCapturing { get; set; }
        private static SKImage? _lastSnapshot;
        
        public static SKImage? GetSnapshot(TopLevel window, SKSurface currentSurface)
        {
            if (IsCapturing) return _lastSnapshot;

            IsCapturing = true;
            try
            {
                _lastSnapshot?.Dispose();
                _lastSnapshot = currentSurface.Snapshot();
                return _lastSnapshot;
            }
            finally
            {
                IsCapturing = false;
            }
        }
    }
}