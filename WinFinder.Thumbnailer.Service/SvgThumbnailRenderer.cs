using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpVectors.Converters;
using SharpVectors.Dom;
using SharpVectors.Renderers.Wpf;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class SvgThumbnailRenderer : IThumbnailRenderer {
        private const int StaRenderTimeoutMs = 600;
        private static readonly object SvgSync = new object();

        public string DecoderId => "svg";
        public string DecoderVersion => "1";
        public int RenderTimeoutMs => 900;

        public static bool IsAvailable {
            get {
                try {
                    return typeof(FileSvgReader) != null;
                } catch {
                    return false;
                }
            }
        }

        public bool CanRender(ProtocolThumbnailRequest request) {
            return request != null
                && IsAvailable
                && string.Equals(NormalizeFormat(request.Format), "SVG", StringComparison.OrdinalIgnoreCase);
        }

        public Bitmap Render(string path, int targetPixels) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                return null;
            }

            try {
                int pixelSize = Math.Max(1, targetPixels <= 0 ? 210 : targetPixels);
                return RunOnStaThread(() => RenderOnSta(path, pixelSize));
            } catch (Exception ex) {
                Trace.WriteLine($"SVG thumbnail failed for {path}: {ex.Message}");
                return null;
            }
        }

        private static Bitmap RenderOnSta(string path, int pixelSize) {
            lock (SvgSync) {
                var settings = new WpfDrawingSettings {
                    IncludeRuntime = false,
                    TextAsGeometry = true,
                    CanUseBitmap = false,
                    ExternalResourcesAccessMode = ExternalResourcesAccessModes.Ignore,
                    PixelWidth = pixelSize,
                    PixelHeight = pixelSize,
                    EnsureViewboxSize = true,
                    EnsureViewboxPosition = true
                };

                using (var reader = new FileSvgReader(settings, false)) {
                    DrawingGroup drawing = reader.Read(path);
                    if (drawing == null) {
                        return null;
                    }

                    drawing.Freeze();
                    return RasterizeDrawing(drawing, pixelSize);
                }
            }
        }

        private static Bitmap RasterizeDrawing(DrawingGroup drawing, int pixelSize) {
            var visual = new DrawingVisual();
            Rect bounds = drawing.Bounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) {
                bounds = new Rect(0, 0, pixelSize, pixelSize);
            }

            double scale = Math.Min(pixelSize / bounds.Width, pixelSize / bounds.Height);
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) {
                scale = 1;
            }

            double drawWidth = bounds.Width * scale;
            double drawHeight = bounds.Height * scale;
            double offsetX = (pixelSize - drawWidth) / 2.0;
            double offsetY = (pixelSize - drawHeight) / 2.0;

            using (DrawingContext context = visual.RenderOpen()) {
                context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, pixelSize, pixelSize));
                context.PushTransform(new TranslateTransform(offsetX, offsetY));
                context.PushTransform(new ScaleTransform(scale, scale));
                context.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
                context.DrawDrawing(drawing);
                context.Pop();
                context.Pop();
                context.Pop();
            }

            var render = new RenderTargetBitmap(pixelSize, pixelSize, 96, 96, PixelFormats.Pbgra32);
            render.Render(visual);
            render.Freeze();
            return BitmapFromBitmapSource(render);
        }

        private static Bitmap BitmapFromBitmapSource(BitmapSource source) {
            if (source == null) {
                return null;
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = new MemoryStream()) {
                encoder.Save(stream);
                stream.Position = 0;
                using (var loaded = new Bitmap(stream)) {
                    return new Bitmap(loaded);
                }
            }
        }

        private static T RunOnStaThread<T>(Func<T> action) {
            T result = default(T);
            Exception error = null;
            var thread = new Thread(() => {
                try {
                    result = action();
                } catch (Exception ex) {
                    error = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            if (!thread.Join(StaRenderTimeoutMs)) {
                throw new TimeoutException($"SVG STA render timed out after {StaRenderTimeoutMs} ms.");
            }

            if (error != null) {
                throw error;
            }

            return result;
        }

        private static string NormalizeFormat(string format) {
            return (format ?? string.Empty).Trim().TrimStart('.').ToUpperInvariant();
        }
    }
}
