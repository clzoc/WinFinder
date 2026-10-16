using System;
using System.Globalization;
using System.IO;
using WinFinder.Thumbnailer.Worker.Common;

namespace WinFinder.Thumbnailer.Worker.Video {
    internal static class Program {
        private static int Main(string[] args) {
            if (WorkerArguments.Has(args, "--capabilities")) {
                Console.WriteLine(WorkerJson.Serialize(new VideoWorkerResponse {
                    Status = FfmpegToolLocator.IsAvailable ? "ready" : "unavailable",
                    Diagnostic = VideoWorkerCapabilities.IsAvailable
                        ? $"ffmpeg={FfmpegToolLocator.FfmpegPath}"
                        : "Bundled ffmpeg.exe was not found.",
                    FfmpegPath = FfmpegToolLocator.FfmpegPath
                }));
                return VideoWorkerCapabilities.IsAvailable ? 0 : 2;
            }

            string requestPath = WorkerArguments.GetOption(args, "--request", null);
            if (string.IsNullOrWhiteSpace(requestPath) || !File.Exists(requestPath)) {
                Console.Error.WriteLine("Missing --request <json>.");
                return 2;
            }

            try {
                var request = WorkerJson.DeserializeFile<VideoWorkerRequest>(requestPath);
                VideoWorkerResponse response = Generate(request);
                Console.WriteLine(WorkerJson.Serialize(response));
                return string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static VideoWorkerResponse Generate(VideoWorkerRequest request) {
            if (request == null) {
                return VideoWorkerResponse.Failed("Empty video worker request.");
            }

            if (!VideoWorkerCapabilities.IsAvailable) {
                return VideoWorkerResponse.Failed("Bundled ffmpeg.exe was not found.");
            }

            if (string.IsNullOrWhiteSpace(request.InputPath) || !File.Exists(request.InputPath)) {
                return VideoWorkerResponse.Failed("Input video does not exist.");
            }

            if (string.IsNullOrWhiteSpace(request.OutputPath)) {
                return VideoWorkerResponse.Failed("Output path is empty.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath));
            int targetPixels = Math.Max(1, request.TargetPixels <= 0 ? 210 : request.TargetPixels);
            int timeoutMs = Math.Max(500, request.TimeoutMs <= 0 ? 3000 : request.TimeoutMs);

            string arguments = string.Join(" ",
                "-hide_banner",
                "-loglevel error",
                "-y",
                "-ss 00:00:01",
                "-i " + WorkerArguments.Quote(request.InputPath),
                "-frames:v 1",
                "-vf " + WorkerArguments.Quote($"thumbnail,scale='min({targetPixels},iw)':-2"),
                WorkerArguments.Quote(request.OutputPath));

            ExternalProcessResult process = ExternalProcessRunner.Run(VideoWorkerCapabilities.FfmpegPath, arguments, timeoutMs);
            if (!process.Started) {
                return VideoWorkerResponse.Failed(process.Diagnostic ?? "Failed to start ffmpeg.");
            }

            if (process.TimedOut) {
                return VideoWorkerResponse.Failed($"ffmpeg timed out after {timeoutMs.ToString(CultureInfo.InvariantCulture)} ms.");
            }

            if (process.ExitCode != 0) {
                return VideoWorkerResponse.Failed(WorkerDiagnostics.FirstLine(
                    $"ffmpeg exited with {process.ExitCode}.",
                    process.StandardError,
                    process.StandardOutput,
                    process.Diagnostic));
            }

            if (!File.Exists(request.OutputPath) || new FileInfo(request.OutputPath).Length <= 0) {
                return VideoWorkerResponse.Failed("ffmpeg produced no thumbnail.");
            }

            return new VideoWorkerResponse {
                Status = "ready",
                OutputPath = request.OutputPath,
                DecoderId = "video-ffmpeg",
                ElapsedMs = process.ElapsedMs,
                FfmpegPath = VideoWorkerCapabilities.FfmpegPath
            };
        }
    }

    internal static class VideoWorkerCapabilities {
        public static string FfmpegPath => FfmpegToolLocator.FfmpegPath;
        public static bool IsAvailable => FfmpegToolLocator.IsAvailable;
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
