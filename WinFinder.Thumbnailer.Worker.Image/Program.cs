using System;
using System.Globalization;
using System.IO;
using WinFinder.Thumbnailer.Worker.Common;

namespace WinFinder.Thumbnailer.Worker.Image {
    internal static class Program {
        private static int Main(string[] args) {
            if (WorkerArguments.Has(args, "--capabilities")) {
                ImageWorkerFormatCapabilities formats = ImageWorkerCapabilities.GetFormatCapabilities();
                Console.WriteLine(WorkerJson.Serialize(new ImageWorkerResponse {
                    Status = ImageWorkerCapabilities.IsAvailable ? "ready" : "unavailable",
                    Diagnostic = ImageWorkerCapabilities.IsAvailable
                        ? $"ffmpeg={ImageWorkerCapabilities.FfmpegPath}"
                        : "Bundled ffmpeg.exe was not found.",
                    FfmpegPath = ImageWorkerCapabilities.FfmpegPath,
                    Capabilities = formats
                }));
                return ImageWorkerCapabilities.IsAvailable ? 0 : 2;
            }

            string requestPath = WorkerArguments.GetOption(args, "--request", null);
            if (string.IsNullOrWhiteSpace(requestPath) || !File.Exists(requestPath)) {
                Console.Error.WriteLine("Missing --request <json>.");
                return 2;
            }

            try {
                var request = WorkerJson.DeserializeFile<ImageWorkerRequest>(requestPath);
                ImageWorkerResponse response = Generate(request);
                Console.WriteLine(WorkerJson.Serialize(response));
                return string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static ImageWorkerResponse Generate(ImageWorkerRequest request) {
            if (request == null) {
                return ImageWorkerResponse.Failed("Empty image worker request.");
            }

            if (!ImageWorkerCapabilities.IsAvailable) {
                return ImageWorkerResponse.Failed("Bundled ffmpeg.exe was not found.");
            }

            if (string.IsNullOrWhiteSpace(request.InputPath) || !File.Exists(request.InputPath)) {
                return ImageWorkerResponse.Failed("Input image does not exist.");
            }

            if (string.IsNullOrWhiteSpace(request.OutputPath)) {
                return ImageWorkerResponse.Failed("Output path is empty.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath));
            int targetPixels = Math.Max(1, request.TargetPixels <= 0 ? 210 : request.TargetPixels);
            int timeoutMs = Math.Max(500, request.TimeoutMs <= 0 ? 2500 : request.TimeoutMs);

            string arguments = string.Join(" ",
                "-hide_banner",
                "-loglevel error",
                "-y",
                "-i " + WorkerArguments.Quote(request.InputPath),
                "-frames:v 1",
                "-vf " + WorkerArguments.Quote($"scale='min({targetPixels},iw)':-2"),
                WorkerArguments.Quote(request.OutputPath));

            ExternalProcessResult process = ExternalProcessRunner.Run(ImageWorkerCapabilities.FfmpegPath, arguments, timeoutMs);
            if (!process.Started) {
                return ImageWorkerResponse.Failed(process.Diagnostic ?? "Failed to start ffmpeg.");
            }

            if (process.TimedOut) {
                return ImageWorkerResponse.Failed($"ffmpeg timed out after {timeoutMs.ToString(CultureInfo.InvariantCulture)} ms.");
            }

            if (process.ExitCode != 0) {
                return ImageWorkerResponse.Failed(WorkerDiagnostics.FirstLine(
                    $"ffmpeg exited with {process.ExitCode}.",
                    process.StandardError,
                    process.StandardOutput,
                    process.Diagnostic));
            }

            if (!File.Exists(request.OutputPath) || new FileInfo(request.OutputPath).Length <= 0) {
                return ImageWorkerResponse.Failed("ffmpeg produced no thumbnail.");
            }

            return new ImageWorkerResponse {
                Status = "ready",
                OutputPath = request.OutputPath,
                DecoderId = "image-ffmpeg",
                ElapsedMs = process.ElapsedMs,
                FfmpegPath = ImageWorkerCapabilities.FfmpegPath
            };
        }
    }

    internal static class ImageWorkerCapabilities {
        public static string FfmpegPath => FfmpegToolLocator.FfmpegPath;
        public static bool IsAvailable => FfmpegToolLocator.IsAvailable;

        public static ImageWorkerFormatCapabilities GetFormatCapabilities() {
            if (!IsAvailable) {
                return new ImageWorkerFormatCapabilities();
            }

            string version = RunFfmpegText("-version", 1000);
            bool av1 = ContainsToken(version, "--enable-libdav1d") || ContainsToken(version, "--enable-libaom");
            bool hevc = true;

            return new ImageWorkerFormatCapabilities {
                Heic = hevc,
                Heif = hevc,
                Avif = av1,
                Webp = ContainsToken(version, "--enable-libwebp"),
                Jxl = ContainsToken(version, "--enable-libjxl"),
                Tiff = true,
                Raw = false
            };
        }

        private static string RunFfmpegText(string arguments, int timeoutMs) {
            ExternalProcessResult process = ExternalProcessRunner.Run(FfmpegPath, arguments, timeoutMs);
            if (!process.Started || process.TimedOut) {
                return string.Empty;
            }

            return (process.StandardOutput + Environment.NewLine + process.StandardError).ToLowerInvariant();
        }

        private static bool ContainsToken(string text, string token) {
            return !string.IsNullOrWhiteSpace(text)
                && text.IndexOf(token ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
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
}
