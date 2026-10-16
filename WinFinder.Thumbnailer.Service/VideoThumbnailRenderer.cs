using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class VideoThumbnailRenderer : IThumbnailRenderer {
        private const int WorkerTimeoutMs = 3000;
        private const int CapabilityCacheMs = 5000;
        private static readonly object CapabilitySync = new object();
        private static bool? cachedIsAvailable;
        private static DateTime cachedIsAvailableUtc = DateTime.MinValue;
        private static readonly WorkerProcessClient<VideoWorkerResponse> WorkerClient =
            new WorkerProcessClient<VideoWorkerResponse>(GetWorkerPath(), "Video", VideoWorkerResponse.Failed);
        private static readonly HashSet<string> VideoFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "MP4", "M4V", "MOV", "MKV", "AVI", "WEBM", "WMV", "MPEG", "MPG", "TS", "M2TS", "3GP", "FLV"
        };

        public string DecoderId => "video-ffmpeg";
        public string DecoderVersion => "1";
        public int RenderTimeoutMs => WorkerTimeoutMs + 750;

        public static bool IsAvailable => WorkerClient.Exists && IsWorkerAvailable();

        public static VideoWorkerResponse GetDiagnosticResponse() {
            return WorkerClient.Exists
                ? WorkerClient.RunCapabilities(1500)
                : VideoWorkerResponse.Failed("Video worker executable was not found.");
        }

        public bool CanRender(ProtocolThumbnailRequest request) {
            return request != null
                && IsAvailable
                && VideoFormats.Contains(NormalizeFormat(request.Format));
        }

        public Bitmap Render(string path, int targetPixels) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                return null;
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "WinFinder.Thumbnailer.Video");
            Directory.CreateDirectory(tempRoot);
            string requestPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + ".json");
            string outputPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + ".png");

            try {
                File.WriteAllText(requestPath, ThumbnailProtocol.Serialize(new VideoWorkerRequest {
                    InputPath = path,
                    OutputPath = outputPath,
                    TargetPixels = targetPixels <= 0 ? 210 : targetPixels,
                    TimeoutMs = WorkerTimeoutMs
                }));

                VideoWorkerResponse response = WorkerClient.RunRequest(requestPath, WorkerTimeoutMs + 500);
                if (response == null || !string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase)) {
                    Trace.WriteLine($"Video worker failed for {path}: {response?.Diagnostic}");
                    return null;
                }

                if (!File.Exists(outputPath)) {
                    return null;
                }

                using (var loaded = new Bitmap(outputPath)) {
                    return new Bitmap(loaded);
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Video thumbnail failed for {path}: {ex.Message}");
                return null;
            } finally {
                TryDelete(requestPath);
                TryDelete(outputPath);
            }
        }

        private static bool IsWorkerAvailable() {
            lock (CapabilitySync) {
                if (cachedIsAvailable.HasValue && (DateTime.UtcNow - cachedIsAvailableUtc).TotalMilliseconds < CapabilityCacheMs) {
                    return cachedIsAvailable.Value;
                }
            }

            bool isAvailable;
            try {
                VideoWorkerResponse response = WorkerClient.RunCapabilities(1000);
                isAvailable = response != null && string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase);
            } catch {
                isAvailable = false;
            }

            lock (CapabilitySync) {
                cachedIsAvailable = isAvailable;
                cachedIsAvailableUtc = DateTime.UtcNow;
            }

            return isAvailable;
        }

        private static string GetWorkerPath() {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workers", "Video", "WinFinder.Thumbnailer.Worker.Video.exe");
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

    internal sealed class VideoWorkerRequest {
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public int TargetPixels { get; set; }
        public int TimeoutMs { get; set; }
    }

    internal sealed class VideoWorkerResponse {
        public string Status { get; set; }
        public string DecoderId { get; set; }
        public string OutputPath { get; set; }
        public string Diagnostic { get; set; }
        public long ElapsedMs { get; set; }
        public string FfmpegPath { get; set; }

        public static VideoWorkerResponse Failed(string diagnostic) {
            return new VideoWorkerResponse {
                Status = "error",
                Diagnostic = diagnostic
            };
        }
    }
}
