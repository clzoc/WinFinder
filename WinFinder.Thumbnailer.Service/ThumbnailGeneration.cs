using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class ThumbnailGenerator {
        private const int DefaultRendererTimeoutMs = 650;
        private const int MinRendererTimeoutMs = 100;
        private const int MaxRendererTimeoutMs = 5000;
        private const int FailureRetryMs = 30000;
        private static readonly TimeSpan BudgetCheckInterval = TimeSpan.FromMinutes(5);
        private static readonly object BudgetSync = new object();
        private static DateTime nextBudgetCheckUtc = DateTime.MinValue;
        private readonly ThumbnailCacheStore cacheStore;
        private readonly IReadOnlyList<IThumbnailRenderer> renderers;

        public ThumbnailGenerator(ThumbnailCacheStore cacheStore) {
            this.cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
            renderers = new IThumbnailRenderer[] {
                new TextThumbnailRenderer(),
                new PdfThumbnailRenderer(),
                new SvgThumbnailRenderer(),
                new VideoThumbnailRenderer(),
                new ImageThumbnailRenderer(),
                new ShellThumbnailRenderer()
            };
        }

        public ThumbnailResponse Generate(ProtocolRequest request) {
            if (request == null || request.Thumbnail == null) {
                return ThumbnailResponse.Unsupported(null, "Empty thumbnail request.");
            }

            ProtocolThumbnailRequest thumbnail = request.Thumbnail;
            if (thumbnail.IsDirectory) {
                return ThumbnailResponse.Unsupported(request.RequestId, "Directory thumbnails are not implemented in the service yet.");
            }

            string normalizedPath = NormalizeFilePath(thumbnail.FullPath);
            if (string.IsNullOrWhiteSpace(normalizedPath) || !File.Exists(normalizedPath)) {
                return ThumbnailResponse.Error(request.RequestId, "Thumbnail source file does not exist.");
            }

            IReadOnlyList<IThumbnailRenderer> acceptedRenderers = SelectRenderers(thumbnail);
            if (acceptedRenderers.Count == 0) {
                return ThumbnailResponse.Unsupported(request.RequestId, "No service renderer accepted the file.");
            }

            string lastDiagnostic = string.Empty;
            foreach (IThumbnailRenderer renderer in acceptedRenderers) {
                CacheIdentity identity = CacheIdentity.Create(normalizedPath, thumbnail.TargetPixels, renderer.DecoderId, renderer.DecoderVersion);
                string pngPath = cacheStore.GetObjectPath(identity.CacheKey, ".png");
                string metadataPath = cacheStore.GetObjectPath(identity.CacheKey, ".json");
                string failurePath = cacheStore.GetFailurePath(identity.CacheKey);

                if (File.Exists(pngPath) && File.Exists(metadataPath)) {
                    ThumbnailCacheStore.TouchMetadataAccess(metadataPath);
                    return ThumbnailResponse.Ready(request.RequestId, "cache", renderer.DecoderId, pngPath, identity.CacheKey, "image/png");
                }

                if (TryReadFreshFailure(failurePath, out ThumbnailCacheMetadata cachedFailure)) {
                    lastDiagnostic = cachedFailure?.Diagnostic ?? $"{renderer.DecoderId} renderer recently failed.";
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
                Directory.CreateDirectory(Path.GetDirectoryName(failurePath));

                RendererExecutionResult execution = ExecuteRenderer(renderer, normalizedPath, thumbnail.TargetPixels, request.DeadlineMs);
                if (execution.Bitmap == null) {
                    lastDiagnostic = execution.Diagnostic;
                    WriteFailureMetadata(failurePath, identity, renderer, normalizedPath, thumbnail.TargetPixels, lastDiagnostic);
                    continue;
                }

                using (execution.Bitmap) {
                    execution.Bitmap.Save(pngPath, ImageFormat.Png);
                }

                WriteSuccessMetadata(metadataPath, identity, renderer, normalizedPath, thumbnail.TargetPixels);
                TryDelete(failurePath);
                EnforceBudgetIfDue();

                return ThumbnailResponse.Ready(request.RequestId, "generated", renderer.DecoderId, pngPath, identity.CacheKey, "image/png");
            }

            return ThumbnailResponse.Unsupported(
                request.RequestId,
                string.IsNullOrWhiteSpace(lastDiagnostic)
                    ? "No service renderer produced an image."
                    : lastDiagnostic);
        }

        private static RendererExecutionResult ExecuteRenderer(IThumbnailRenderer renderer, string normalizedPath, int targetPixels, int deadlineMs) {
            int timeoutMs = CalculateRendererTimeoutMs(renderer, deadlineMs);
            Task<Bitmap> renderTask = Task.Run(() => renderer.Render(normalizedPath, targetPixels));
            try {
                if (!renderTask.Wait(timeoutMs)) {
                    return RendererExecutionResult.Failed($"{renderer.DecoderId} renderer timed out after {timeoutMs} ms.");
                }

                Bitmap bitmap = renderTask.Result;
                return bitmap == null
                    ? RendererExecutionResult.Failed($"{renderer.DecoderId} renderer returned no image.")
                    : RendererExecutionResult.Ready(bitmap);
            } catch (AggregateException ex) {
                Exception inner = ex.Flatten().InnerException ?? ex;
                return RendererExecutionResult.Failed($"{renderer.DecoderId} renderer failed: {inner.Message}");
            } catch (Exception ex) {
                return RendererExecutionResult.Failed($"{renderer.DecoderId} renderer failed: {ex.Message}");
            }
        }

        private static int CalculateRendererTimeoutMs(IThumbnailRenderer renderer, int deadlineMs) {
            int rendererBudget = renderer == null || renderer.RenderTimeoutMs <= 0
                ? DefaultRendererTimeoutMs
                : renderer.RenderTimeoutMs;

            rendererBudget = Math.Min(MaxRendererTimeoutMs, Math.Max(MinRendererTimeoutMs, rendererBudget));
            if (deadlineMs <= 0) {
                return rendererBudget;
            }

            int requestBudget = Math.Max(MinRendererTimeoutMs, deadlineMs - 75);
            return Math.Min(rendererBudget, requestBudget);
        }

        private void EnforceBudgetIfDue() {
            lock (BudgetSync) {
                if (DateTime.UtcNow < nextBudgetCheckUtc) {
                    return;
                }

                nextBudgetCheckUtc = DateTime.UtcNow.Add(BudgetCheckInterval);
            }

            Task.Run(() => {
                try {
                    cacheStore.EnforceBudget();
                } catch {
                }
            });
        }

        private static void WriteSuccessMetadata(string metadataPath, CacheIdentity identity, IThumbnailRenderer renderer, string normalizedPath, int targetPixels) {
            long nowTicks = DateTime.UtcNow.Ticks;
            File.WriteAllText(metadataPath, ThumbnailProtocol.Serialize(new ThumbnailCacheMetadata {
                CacheKey = identity.CacheKey,
                Status = "ready",
                NormalizedPath = normalizedPath,
                LastWriteUtcTicks = identity.LastWriteUtcTicks,
                Length = identity.Length,
                TargetPixels = targetPixels,
                DecoderId = renderer.DecoderId,
                DecoderVersion = renderer.DecoderVersion,
                CreatedUtcTicks = nowTicks,
                LastAccessUtcTicks = nowTicks
            }));
        }

        private static void WriteFailureMetadata(string failurePath, CacheIdentity identity, IThumbnailRenderer renderer, string normalizedPath, int targetPixels, string diagnostic) {
            File.WriteAllText(failurePath, ThumbnailProtocol.Serialize(new ThumbnailCacheMetadata {
                CacheKey = identity.CacheKey,
                Status = "failed",
                NormalizedPath = normalizedPath,
                LastWriteUtcTicks = identity.LastWriteUtcTicks,
                Length = identity.Length,
                TargetPixels = targetPixels,
                DecoderId = renderer.DecoderId,
                DecoderVersion = renderer.DecoderVersion,
                Diagnostic = diagnostic,
                CreatedUtcTicks = DateTime.UtcNow.Ticks,
                RetryAfterUtcTicks = DateTime.UtcNow.AddMilliseconds(FailureRetryMs).Ticks
            }));
        }

        private static bool TryReadFreshFailure(string failurePath, out ThumbnailCacheMetadata metadata) {
            metadata = null;
            if (string.IsNullOrWhiteSpace(failurePath) || !File.Exists(failurePath)) {
                return false;
            }

            try {
                metadata = ThumbnailProtocol.Deserialize<ThumbnailCacheMetadata>(File.ReadAllText(failurePath));
                if (metadata == null || !string.Equals(metadata.Status, "failed", StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }

                if (metadata.RetryAfterUtcTicks <= DateTime.UtcNow.Ticks) {
                    TryDelete(failurePath);
                    return false;
                }

                return true;
            } catch {
                TryDelete(failurePath);
                return false;
            }
        }

        private static void TryDelete(string path) {
            try {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
                    File.Delete(path);
                }
            } catch {
            }
        }

        private IReadOnlyList<IThumbnailRenderer> SelectRenderers(ProtocolThumbnailRequest thumbnail) {
            var accepted = new List<IThumbnailRenderer>();
            foreach (IThumbnailRenderer renderer in renderers) {
                if (renderer.CanRender(thumbnail)) {
                    accepted.Add(renderer);
                }
            }

            return accepted;
        }

        private static string NormalizeFilePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            try {
                return Path.GetFullPath(path.Trim());
            } catch {
                return path.Trim();
            }
        }
    }

    internal interface IThumbnailRenderer {
        string DecoderId { get; }
        string DecoderVersion { get; }
        int RenderTimeoutMs { get; }
        bool CanRender(ProtocolThumbnailRequest request);
        Bitmap Render(string path, int targetPixels);
    }

    internal sealed class RendererExecutionResult {
        private RendererExecutionResult(Bitmap bitmap, string diagnostic) {
            Bitmap = bitmap;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public Bitmap Bitmap { get; }
        public string Diagnostic { get; }

        public static RendererExecutionResult Ready(Bitmap bitmap) {
            return new RendererExecutionResult(bitmap, null);
        }

        public static RendererExecutionResult Failed(string diagnostic) {
            return new RendererExecutionResult(null, diagnostic);
        }
    }

    internal sealed class TextThumbnailRenderer : IThumbnailRenderer {
        private static readonly HashSet<string> TextFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "BAT", "H", "LICENSE", "README", "TXT", "XML", "XAML", "LOG", "CUB", "CSS",
            "CSV", "IPYNB", "GBS", "PDB", "PY", "CPP", "HTML", "C", "AHK", "MD",
            "YAML", "JSON", "JS", "TS"
        };

        public string DecoderId => "text";
        public string DecoderVersion => "1";
        public int RenderTimeoutMs => 650;

        public bool CanRender(ProtocolThumbnailRequest request) {
            if (request == null) {
                return false;
            }

            string format = NormalizeFormat(request.Format);
            string name = request.Name ?? string.Empty;
            bool isHiddenExtensionFile = name.Length > 1
                && name[0] == '.'
                && string.Equals(name.Substring(1), format, StringComparison.OrdinalIgnoreCase);

            return isHiddenExtensionFile || TextFormats.Contains(format);
        }

        public Bitmap Render(string path, int targetPixels) {
            string previewText = ReadTextPreview(path, 60);
            if (previewText == null) {
                return null;
            }

            const int width = 450;
            const int height = 600;
            const int padding = 20;

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.Clear(Color.White);
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Font font;
                try {
                    font = new Font("Microsoft YaHei UI", 18, FontStyle.Regular, GraphicsUnit.Point);
                } catch {
                    font = new Font("Segoe UI", 18, FontStyle.Regular, GraphicsUnit.Point);
                }

                using (font)
                using (var brush = new SolidBrush(Color.Black))
                using (var format = new StringFormat(StringFormat.GenericDefault)) {
                    format.Trimming = StringTrimming.None;
                    format.FormatFlags |= StringFormatFlags.LineLimit;
                    var layout = new RectangleF(padding, padding, width - padding * 2, height - padding * 2);
                    graphics.DrawString(previewText, font, brush, layout, format);
                }
            }

            return bitmap;
        }

        private static string ReadTextPreview(string path, int maxLines) {
            try {
                using (var reader = new StreamReader(path, Encoding.UTF8, true)) {
                    var builder = new StringBuilder();
                    string line;
                    int count = 0;
                    while (count < maxLines && (line = reader.ReadLine()) != null) {
                        if (count > 0) {
                            builder.AppendLine();
                        }
                        builder.Append(line);
                        count++;
                    }
                    return builder.ToString();
                }
            } catch (IOException) {
                return null;
            } catch (UnauthorizedAccessException) {
                return null;
            }
        }

        private static string NormalizeFormat(string format) {
            return (format ?? string.Empty).Trim().TrimStart('.').ToUpperInvariant();
        }
    }

    internal sealed class CacheIdentity {
        public string CacheKey { get; private set; }
        public long LastWriteUtcTicks { get; private set; }
        public long Length { get; private set; }

        public static CacheIdentity Create(string normalizedPath, int targetPixels, string decoderId, string decoderVersion) {
            var fileInfo = new FileInfo(normalizedPath);
            string material = string.Join("|",
                normalizedPath,
                fileInfo.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                fileInfo.Length.ToString(CultureInfo.InvariantCulture),
                Math.Max(1, targetPixels).ToString(CultureInfo.InvariantCulture),
                decoderId ?? string.Empty,
                decoderVersion ?? string.Empty);

            return new CacheIdentity {
                CacheKey = Sha256Hex(material),
                LastWriteUtcTicks = fileInfo.LastWriteTimeUtc.Ticks,
                Length = fileInfo.Length
            };
        }

        private static string Sha256Hex(string text) {
            using (SHA256 sha = SHA256.Create()) {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) {
                    builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }
    }

    internal sealed class ProtocolThumbnailRequest {
        public string FullPath { get; set; }
        public string Name { get; set; }
        public string Format { get; set; }
        public bool IsDirectory { get; set; }
        public int TargetPixels { get; set; }
    }

    internal sealed class ThumbnailResponse {
        public int ProtocolVersion { get; set; }
        public string RequestId { get; set; }
        public string Status { get; set; }
        public string Source { get; set; }
        public string DecoderId { get; set; }
        public string MimeType { get; set; }
        public string CacheKey { get; set; }
        public string CachePath { get; set; }
        public string Diagnostic { get; set; }

        public static ThumbnailResponse Ready(string requestId, string source, string decoderId, string cachePath, string cacheKey, string mimeType) {
            return new ThumbnailResponse {
                ProtocolVersion = ThumbnailProtocol.CurrentProtocolVersion,
                RequestId = requestId,
                Status = "ready",
                Source = source,
                DecoderId = decoderId,
                CachePath = cachePath,
                CacheKey = cacheKey,
                MimeType = mimeType
            };
        }

        public static ThumbnailResponse Unsupported(string requestId, string diagnostic) {
            return new ThumbnailResponse {
                ProtocolVersion = ThumbnailProtocol.CurrentProtocolVersion,
                RequestId = requestId,
                Status = "unsupported",
                Source = "unsupported",
                Diagnostic = diagnostic
            };
        }

        public static ThumbnailResponse Error(string requestId, string diagnostic) {
            return new ThumbnailResponse {
                ProtocolVersion = ThumbnailProtocol.CurrentProtocolVersion,
                RequestId = requestId,
                Status = "error",
                Source = "error",
                Diagnostic = diagnostic
            };
        }
    }

    internal sealed class ThumbnailCacheMetadata {
        public string CacheKey { get; set; }
        public string Status { get; set; }
        public string NormalizedPath { get; set; }
        public long LastWriteUtcTicks { get; set; }
        public long Length { get; set; }
        public int TargetPixels { get; set; }
        public string DecoderId { get; set; }
        public string DecoderVersion { get; set; }
        public string Diagnostic { get; set; }
        public long CreatedUtcTicks { get; set; }
        public long LastAccessUtcTicks { get; set; }
        public long RetryAfterUtcTicks { get; set; }
    }
}
