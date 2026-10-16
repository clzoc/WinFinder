using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class ImageThumbnailRenderer : IThumbnailRenderer {
        private const int WorkerTimeoutMs = 2500;
        private const int CapabilityCacheMs = 5000;
        private static readonly object CapabilitySync = new object();
        private static ImageWorkerFormatCapabilities cachedCapabilities;
        private static DateTime cachedCapabilitiesUtc = DateTime.MinValue;
        private static readonly WorkerProcessClient<ImageWorkerResponse> WorkerClient =
            new WorkerProcessClient<ImageWorkerResponse>(GetWorkerPath(), "Image", ImageWorkerResponse.Failed);
        private static readonly HashSet<string> ImageFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "HEIC", "HEIF", "AVIF", "WEBP", "JXL", "TIFF", "TIF"
        };

        public string DecoderId => "image-ffmpeg";
        public string DecoderVersion => "1";
        public int RenderTimeoutMs => WorkerTimeoutMs + 750;

        public static bool IsAvailable => WorkerClient.Exists && IsWorkerAvailable();
        public static ImageWorkerFormatCapabilities Capabilities => GetCapabilities();

        public static ImageWorkerResponse GetDiagnosticResponse() {
            return WorkerClient.Exists
                ? WorkerClient.RunCapabilities(1500)
                : ImageWorkerResponse.Failed("Image worker executable was not found.");
        }

        public bool CanRender(ProtocolThumbnailRequest request) {
            return request != null
                && IsAvailable
                && ImageFormats.Contains(NormalizeFormat(request.Format));
        }

        public Bitmap Render(string path, int targetPixels) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                return null;
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "WinFinder.Thumbnailer.Image");
            Directory.CreateDirectory(tempRoot);
            string requestPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + ".json");
            string outputPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + ".png");

            try {
                File.WriteAllText(requestPath, ThumbnailProtocol.Serialize(new ImageWorkerRequest {
                    InputPath = path,
                    OutputPath = outputPath,
                    TargetPixels = targetPixels <= 0 ? 210 : targetPixels,
                    TimeoutMs = WorkerTimeoutMs
                }));

                ImageWorkerResponse response = WorkerClient.RunRequest(requestPath, WorkerTimeoutMs + 500);
                if (response == null || !string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase)) {
                    Trace.WriteLine($"Image worker failed for {path}: {response?.Diagnostic}");
                    return null;
                }

                if (!File.Exists(outputPath)) {
                    return null;
                }

                using (var loaded = new Bitmap(outputPath)) {
                    return new Bitmap(loaded);
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Image thumbnail failed for {path}: {ex.Message}");
                return null;
            } finally {
                TryDelete(requestPath);
                TryDelete(outputPath);
            }
        }

        private static bool IsWorkerAvailable() {
            ImageWorkerFormatCapabilities capabilities = GetCapabilities();
            return capabilities.Heic || capabilities.Avif || capabilities.Webp || capabilities.Jxl || capabilities.Tiff;
        }

        private static ImageWorkerFormatCapabilities GetCapabilities() {
            lock (CapabilitySync) {
                if (cachedCapabilities != null && (DateTime.UtcNow - cachedCapabilitiesUtc).TotalMilliseconds < CapabilityCacheMs) {
                    return cachedCapabilities;
                }
            }

            ImageWorkerFormatCapabilities capabilities;
            try {
                ImageWorkerResponse response = WorkerClient.RunCapabilities(1000);
                if (response == null || !string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase)) {
                    capabilities = new ImageWorkerFormatCapabilities();
                } else {
                    capabilities = response.Capabilities ?? new ImageWorkerFormatCapabilities();
                }
            } catch {
                capabilities = new ImageWorkerFormatCapabilities();
            }

            lock (CapabilitySync) {
                cachedCapabilities = capabilities;
                cachedCapabilitiesUtc = DateTime.UtcNow;
            }

            return capabilities;
        }

        private static string GetWorkerPath() {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workers", "Image", "WinFinder.Thumbnailer.Worker.Image.exe");
        }

        private static void TryDelete(string path) {
            try {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
                    File.Delete(path);
                }
            } catch {
            }
        }

        private static string NormalizeFormat(string format) {
            return (format ?? string.Empty).Trim().TrimStart('.').ToUpperInvariant();
        }
    }

    internal sealed class ImageWorkerRequest {
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public int TargetPixels { get; set; }
        public int TimeoutMs { get; set; }
    }

    internal sealed class ImageWorkerResponse {
        public string Status { get; set; }
        public string DecoderId { get; set; }
        public string OutputPath { get; set; }
        public string Diagnostic { get; set; }
        public long ElapsedMs { get; set; }
        public string FfmpegPath { get; set; }
        public ImageWorkerFormatCapabilities Capabilities { get; set; }

        public static ImageWorkerResponse Failed(string diagnostic) {
            return new ImageWorkerResponse {
                Status = "error",
                Diagnostic = diagnostic
            };
        }
    }

    internal sealed class ImageWorkerFormatCapabilities {
        public bool Heic { get; set; }
        public bool Heif { get; set; }
        public bool Avif { get; set; }
        public bool Webp { get; set; }
        public bool Jxl { get; set; }
        public bool Tiff { get; set; }
        public bool Raw { get; set; }
    }
}
