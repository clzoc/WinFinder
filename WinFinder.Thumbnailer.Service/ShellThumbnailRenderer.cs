using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Shell;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class ShellThumbnailRenderer : IThumbnailRenderer {
        private const int StaRenderTimeoutMs = 600;

        public string DecoderId => "shell";
        public string DecoderVersion => "1";
        public int RenderTimeoutMs => 900;

        public static bool IsAvailable {
            get {
                try {
                    return Type.GetType("Microsoft.WindowsAPICodePack.Shell.ShellObject, Microsoft.WindowsAPICodePack.Shell") != null;
                } catch {
                    return false;
                }
            }
        }

        public bool CanRender(ProtocolThumbnailRequest request) {
            return request != null
                && IsAvailable
                && !request.IsDirectory
                && !string.IsNullOrWhiteSpace(request.FullPath);
        }

        public Bitmap Render(string path, int targetPixels) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                return null;
            }

            try {
                return RunOnStaThread(() => LoadShellThumbnail(path));
            } catch (Exception ex) {
                Trace.WriteLine($"Shell thumbnail failed for {path}: {ex.Message}");
                return null;
            }
        }

        private static Bitmap LoadShellThumbnail(string path) {
            using (ShellObject shellObject = ShellObject.FromParsingName(path)) {
                BitmapSource source = shellObject.Thumbnail.LargeBitmapSource;
                if (source == null) {
                    return null;
                }

                if (source.CanFreeze) {
                    source.Freeze();
                }

                return BitmapFromBitmapSource(source);
            }
        }

        private static Bitmap BitmapFromBitmapSource(BitmapSource source) {
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
                throw new TimeoutException($"Shell STA render timed out after {StaRenderTimeoutMs} ms.");
            }

            if (error != null) {
                throw error;
            }

            return result;
        }
    }
}
