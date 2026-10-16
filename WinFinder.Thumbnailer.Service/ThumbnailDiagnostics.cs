using System;
using System.Collections.Generic;
using System.IO;

namespace WinFinder.Thumbnailer.Service {
    internal static class ThumbnailDiagnostics {
        public static int Run() {
            Console.WriteLine(ThumbnailProtocol.Serialize(CreateReport()));
            return 0;
        }

        private static ThumbnailDiagnosticReport CreateReport() {
            ImageWorkerResponse image = ImageThumbnailRenderer.GetDiagnosticResponse();
            VideoWorkerResponse video = VideoThumbnailRenderer.GetDiagnosticResponse();
            CacheStatus cache = GetCacheStatus();

            return new ThumbnailDiagnosticReport {
                ProtocolVersion = ThumbnailProtocol.CurrentProtocolVersion,
                GeneratedUtc = DateTime.UtcNow.ToString("O"),
                Is64BitProcess = Environment.Is64BitProcess,
                BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Cache = cache,
                Dependencies = new ThumbnailDependencyDiagnostics {
                    Ffmpeg = new FfmpegDiagnostic {
                        IsAvailable = IsReady(image) || IsReady(video),
                        ImageWorkerStatus = image?.Status,
                        VideoWorkerStatus = video?.Status,
                        ImageFfmpegPath = image?.FfmpegPath,
                        VideoFfmpegPath = video?.FfmpegPath,
                        ImageDiagnostic = image?.Diagnostic,
                        VideoDiagnostic = video?.Diagnostic,
                        Capabilities = image?.Capabilities
                    },
                    Pdfium = new DependencyDiagnostic {
                        IsAvailable = PdfThumbnailRenderer.IsAvailable,
                        Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfium.dll"),
                        Diagnostic = PdfThumbnailRenderer.IsAvailable
                            ? "pdfium.dll is available."
                            : "pdfium.dll is missing or the process is not x64."
                    },
                    Svg = new DependencyDiagnostic {
                        IsAvailable = SvgThumbnailRenderer.IsAvailable,
                        Diagnostic = SvgThumbnailRenderer.IsAvailable
                            ? "SharpVectors is available."
                            : "SharpVectors is unavailable."
                    },
                    ShellFallback = new DependencyDiagnostic {
                        IsAvailable = ShellThumbnailRenderer.IsAvailable,
                        Diagnostic = ShellThumbnailRenderer.IsAvailable
                            ? "Windows Shell fallback is available."
                            : "Windows Shell fallback is unavailable."
                    }
                },
                Capabilities = ThumbnailCapabilities.Current()
            };
        }

        private static CacheStatus GetCacheStatus() {
            try {
                return new ThumbnailCacheStore().EnsureReady();
            } catch (Exception ex) {
                return new CacheStatus {
                    RootPath = new ThumbnailCacheStore().RootPath,
                    Version = "v1",
                    IsWritable = false,
                    Diagnostic = ex.Message
                };
            }
        }

        private static bool IsReady(VideoWorkerResponse response) {
            return response != null && string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReady(ImageWorkerResponse response) {
            return response != null && string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class ThumbnailDiagnosticReport {
        public int ProtocolVersion { get; set; }
        public string GeneratedUtc { get; set; }
        public bool Is64BitProcess { get; set; }
        public string BaseDirectory { get; set; }
        public CacheStatus Cache { get; set; }
        public ThumbnailDependencyDiagnostics Dependencies { get; set; }
        public ThumbnailCapabilities Capabilities { get; set; }
    }

    internal sealed class ThumbnailDependencyDiagnostics {
        public FfmpegDiagnostic Ffmpeg { get; set; }
        public DependencyDiagnostic Pdfium { get; set; }
        public DependencyDiagnostic Svg { get; set; }
        public DependencyDiagnostic ShellFallback { get; set; }
    }

    internal sealed class FfmpegDiagnostic {
        public bool IsAvailable { get; set; }
        public string ImageWorkerStatus { get; set; }
        public string VideoWorkerStatus { get; set; }
        public string ImageFfmpegPath { get; set; }
        public string VideoFfmpegPath { get; set; }
        public string ImageDiagnostic { get; set; }
        public string VideoDiagnostic { get; set; }
        public ImageWorkerFormatCapabilities Capabilities { get; set; }
    }

    internal sealed class DependencyDiagnostic {
        public bool IsAvailable { get; set; }
        public string Path { get; set; }
        public string Diagnostic { get; set; }
    }
}
