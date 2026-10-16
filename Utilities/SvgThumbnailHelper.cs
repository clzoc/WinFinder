using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SharpVectors.Converters;

namespace WinFinder.Utilities {
    public static class SvgThumbnailHelper {
        public static BitmapSource RenderSvg(string svgPath, int targetPixels = 210) {
            if (string.IsNullOrWhiteSpace(svgPath) || !File.Exists(svgPath)) {
                return null;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) {
                return null;
            }

            BitmapSource result = null;
            Action renderAction = () => {
                var svgBox = new SvgViewbox {
                    Source = new Uri(svgPath, UriKind.Absolute),
                    Stretch = Stretch.Uniform,
                    Width = targetPixels,
                    Height = targetPixels,
                    SnapsToDevicePixels = true
                };

                svgBox.Measure(new Size(targetPixels, targetPixels));
                svgBox.Arrange(new Rect(0, 0, targetPixels, targetPixels));
                svgBox.UpdateLayout();

                var render = new RenderTargetBitmap(targetPixels, targetPixels, 96, 96, PixelFormats.Pbgra32);
                render.Render(svgBox);
                render.Freeze();

                result = render;
            };

            try {
                if (dispatcher.CheckAccess()) {
                    renderAction();
                } else {
                    dispatcher.Invoke(renderAction, DispatcherPriority.Background);
                }
            } catch (Exception ex) {
                Trace.WriteLine($"RenderSvg failed for {svgPath}: {ex.Message}");
                return null;
            }

            return result;
        }

        public static void SaveAsPng(BitmapSource bitmap, string outputPath) {
            if (bitmap == null || string.IsNullOrWhiteSpace(outputPath)) {
                return;
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                encoder.Save(stream);
            }
        }
    }
}
