using System;
using System.Threading;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace WinFinder.Backend {
    public enum ThumbnailResultStatus {
        Empty,
        Pending,
        Ready,
        Fallback,
        Unsupported,
        Error
    }

    public enum ThumbnailResultSource {
        None,
        MemoryCache,
        GenericIcon,
        TextPreview,
        Pdfium,
        Svg,
        Shell,
        ServiceCache,
        ServiceGenerated,
        Error
    }

    public enum ThumbnailPriority {
        Background = 0,
        Prefetch = 25,
        Visible = 50,
        Immediate = 100
    }

    public sealed class ThumbnailRequest {
        public ThumbnailRequest(
            string fullPath,
            string name,
            string format,
            bool isDirectory,
            int targetPixels = 210,
            ThumbnailPriority priority = ThumbnailPriority.Visible,
            int deadlineMs = 4000,
            CancellationToken cancellationToken = default(CancellationToken)) {
            FullPath = fullPath ?? string.Empty;
            Name = name ?? string.Empty;
            Format = format ?? string.Empty;
            IsDirectory = isDirectory;
            TargetPixels = targetPixels <= 0 ? 210 : targetPixels;
            Priority = priority;
            DeadlineMs = deadlineMs <= 0 ? 4000 : deadlineMs;
            CancellationToken = cancellationToken;
        }

        public string FullPath { get; }
        public string Name { get; }
        public string Format { get; }
        public bool IsDirectory { get; }
        public int TargetPixels { get; }
        public ThumbnailPriority Priority { get; }
        public int DeadlineMs { get; }
        [JsonIgnore]
        public CancellationToken CancellationToken { get; }
    }

    public sealed class ThumbnailResult {
        private ThumbnailResult(ThumbnailResultStatus status, ThumbnailResultSource source, BitmapSource image, string cacheKey, string diagnostic) {
            Status = status;
            Source = source;
            Image = image;
            CacheKey = cacheKey ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public ThumbnailResultStatus Status { get; }
        public ThumbnailResultSource Source { get; }
        public BitmapSource Image { get; }
        public string CacheKey { get; }
        public string Diagnostic { get; }

        public bool HasImage => Image != null;
        public bool IsTerminal => Status != ThumbnailResultStatus.Empty && Status != ThumbnailResultStatus.Pending;

        public static ThumbnailResult Empty(string diagnostic = null) {
            return new ThumbnailResult(ThumbnailResultStatus.Empty, ThumbnailResultSource.None, null, string.Empty, diagnostic);
        }

        public static ThumbnailResult Pending(string cacheKey = null, string diagnostic = null) {
            return new ThumbnailResult(ThumbnailResultStatus.Pending, ThumbnailResultSource.None, null, cacheKey, diagnostic);
        }

        public static ThumbnailResult Ready(BitmapSource image, ThumbnailResultSource source, string cacheKey = null, string diagnostic = null) {
            return new ThumbnailResult(ThumbnailResultStatus.Ready, source, image, cacheKey, diagnostic);
        }

        public static ThumbnailResult Fallback(BitmapSource image, ThumbnailResultSource source, string cacheKey = null, string diagnostic = null) {
            return new ThumbnailResult(ThumbnailResultStatus.Fallback, source, image, cacheKey, diagnostic);
        }

        public static ThumbnailResult Unsupported(string cacheKey = null, string diagnostic = null) {
            return new ThumbnailResult(ThumbnailResultStatus.Unsupported, ThumbnailResultSource.None, null, cacheKey, diagnostic);
        }

        public static ThumbnailResult Error(string cacheKey = null, string diagnostic = null) {
            return new ThumbnailResult(ThumbnailResultStatus.Error, ThumbnailResultSource.Error, null, cacheKey, diagnostic);
        }
    }

    public sealed class ThumbnailPathIdentity {
        public ThumbnailPathIdentity(string fullPath, string cacheKey) {
            FullPath = fullPath ?? string.Empty;
            CacheKey = cacheKey ?? string.Empty;
        }

        public string FullPath { get; }
        public string CacheKey { get; }
    }
}
