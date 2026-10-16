using Microsoft.WindowsAPICodePack.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WinFinder.Utilities;

namespace WinFinder.Backend {
    public interface IThumbnailService {
        BitmapSource GetThumbnail(ThumbnailRequest request);
        ThumbnailResult GetThumbnailResult(ThumbnailRequest request);
    }

    internal interface IThumbnailProvider {
        ThumbnailResult TryGetThumbnail(ThumbnailRequest request, ThumbnailPathIdentity identity, ThumbnailProviderContext context);
    }

    internal sealed class ThumbnailProviderContext {
        public ThumbnailProviderContext(LruBitmapCache cache, IPdfThumbnailRenderer pdfRenderer) {
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            PdfRenderer = pdfRenderer ?? throw new ArgumentNullException(nameof(pdfRenderer));
        }

        public LruBitmapCache Cache { get; }
        public IPdfThumbnailRenderer PdfRenderer { get; }
    }

    public sealed class ThumbnailService : IThumbnailService {
        private const long ThumbnailCacheLimitBytes = 200L * 1024 * 1024;

        private readonly LruBitmapCache thumbnailCache = new LruBitmapCache(ThumbnailCacheLimitBytes, StringComparer.OrdinalIgnoreCase);
        private readonly IPdfThumbnailRenderer pdfRenderer = new PdfiumThumbnailRenderer();
        private readonly IReadOnlyList<IThumbnailProvider> providers;

        public ThumbnailService() {
            providers = new IThumbnailProvider[] {
                new StaticIconThumbnailProvider(),
                new TextThumbnailProvider(),
                new PdfThumbnailProvider(),
                new SvgThumbnailProvider(),
                new ShellThumbnailProvider(),
                new DefaultFileIconThumbnailProvider()
            };
        }

        public BitmapSource GetThumbnail(ThumbnailRequest request) {
            return GetThumbnailResult(request).Image;
        }

        public ThumbnailResult GetThumbnailResult(ThumbnailRequest request) {
            if (request == null || string.IsNullOrWhiteSpace(request.FullPath)) {
                return ThumbnailResult.Empty("Empty thumbnail request.");
            }

            if (request.CancellationToken.IsCancellationRequested) {
                return ThumbnailResult.Pending(diagnostic: "Thumbnail request was cancelled.");
            }

            ThumbnailPathIdentity identity = ThumbnailPathTools.CreateIdentity(request.FullPath);
            if (string.IsNullOrWhiteSpace(identity.FullPath)) {
                return ThumbnailResult.Empty("Empty thumbnail path.");
            }

            if (thumbnailCache.TryGet(identity.CacheKey, out BitmapSource cachedThumb)) {
                return ThumbnailResult.Ready(cachedThumb, ThumbnailResultSource.MemoryCache, identity.CacheKey);
            }

            var context = new ThumbnailProviderContext(thumbnailCache, pdfRenderer);
            foreach (IThumbnailProvider provider in providers) {
                if (request.CancellationToken.IsCancellationRequested) {
                    return ThumbnailResult.Pending(identity.CacheKey, "Thumbnail request was cancelled.");
                }

                ThumbnailResult result;
                try {
                    result = provider.TryGetThumbnail(request, identity, context);
                } catch (Exception ex) {
                    Trace.WriteLine($"{identity.FullPath} thumbnail provider failed: {ex.Message}");
                    result = ThumbnailResult.Error(identity.CacheKey, ex.Message);
                }

                if (result == null || result.Status == ThumbnailResultStatus.Empty) {
                    continue;
                }

                return result;
            }

            return ThumbnailResult.Unsupported(identity.CacheKey, "No thumbnail provider accepted the file.");
        }

        private sealed class StaticIconThumbnailProvider : IThumbnailProvider {
            public ThumbnailResult TryGetThumbnail(ThumbnailRequest request, ThumbnailPathIdentity identity, ThumbnailProviderContext context) {
                return StaticIconCatalog.TryGetStaticIcon(request.Format, request.IsDirectory, identity.CacheKey)
                    ?? ThumbnailResult.Empty();
            }
        }

        private sealed class TextThumbnailProvider : IThumbnailProvider {
            private static readonly HashSet<string> TextFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "BAT", "H", "LICENSE", "README", "TXT", "XML", "XAML", "LOG", "CUB", "CSS",
                "CSV", "IPYNB", "GBS", "PDB", "PY", "CPP", "HTML", "C", "AHK", "MD",
                "YAML", "JSON", "JS", "TS"
            };

            public ThumbnailResult TryGetThumbnail(ThumbnailRequest request, ThumbnailPathIdentity identity, ThumbnailProviderContext context) {
                string format = ThumbnailPathTools.NormalizeFormat(request.Format);
                if (!IsTextPreviewRequest(request, format)) {
                    return ThumbnailResult.Empty();
                }

                return ThumbnailRenderingTools.GetOrCreateCachedThumbnail(
                    identity,
                    context.Cache,
                    ThumbnailResultSource.TextPreview,
                    () => {
                        string previewText = ReadTextPreview(identity.FullPath, 60);
                        return previewText == null ? null : RenderTextThumbnail(previewText);
                    });
            }

            private static bool IsTextPreviewRequest(ThumbnailRequest request, string format) {
                string name = request.Name ?? string.Empty;
                bool isHiddenExtensionFile = name.Length > 1
                    && name[0] == '.'
                    && string.Equals(name.Substring(1), format, StringComparison.OrdinalIgnoreCase);

                return isHiddenExtensionFile || TextFormats.Contains(format);
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
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    Trace.WriteLine($"{path} {ex.Message}");
                    return null;
                }
            }

            private static BitmapSource RenderTextThumbnail(string text) {
                try {
                    const double width = 450;
                    const double height = 600;
                    const double padding = 20;

                    using (var bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppPArgb)) {
                        using (Graphics graphics = Graphics.FromImage(bitmap)) {
                            graphics.Clear(Color.White);
                            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                            Font font;
                            try {
                                font = new Font("Microsoft YaHei UI", 18, System.Drawing.FontStyle.Regular, GraphicsUnit.Point);
                            } catch {
                                font = new Font("Segoe UI", 18, System.Drawing.FontStyle.Regular, GraphicsUnit.Point);
                            }

                            using (font)
                            using (var brush = new SolidBrush(Color.Black))
                            using (var format = new StringFormat(StringFormat.GenericDefault)) {
                                format.Trimming = StringTrimming.None;
                                format.FormatFlags |= StringFormatFlags.LineLimit;
                                var layout = new RectangleF((float)padding, (float)padding, (float)(width - padding * 2), (float)(height - padding * 2));
                                graphics.DrawString(text ?? string.Empty, font, brush, layout, format);
                            }
                        }

                        IntPtr hBitmap = bitmap.GetHbitmap();
                        try {
                            var source = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            source.Freeze();
                            return source;
                        } finally {
                            ThumbnailRenderingTools.DeleteObject(hBitmap);
                        }
                    }
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    Trace.WriteLine($"{ex.Message}");
                    return null;
                }
            }
        }

        private sealed class PdfThumbnailProvider : IThumbnailProvider {
            public ThumbnailResult TryGetThumbnail(ThumbnailRequest request, ThumbnailPathIdentity identity, ThumbnailProviderContext context) {
                if (!string.Equals(ThumbnailPathTools.NormalizeFormat(request.Format), "PDF", StringComparison.OrdinalIgnoreCase)) {
                    return ThumbnailResult.Empty();
                }

                return ThumbnailRenderingTools.GetOrCreateCachedThumbnail(
                    identity,
                    context.Cache,
                    ThumbnailResultSource.Pdfium,
                    () => context.PdfRenderer.RenderFirstPage(identity.FullPath, request.TargetPixels));
            }
        }

        private sealed class SvgThumbnailProvider : IThumbnailProvider {
            public ThumbnailResult TryGetThumbnail(ThumbnailRequest request, ThumbnailPathIdentity identity, ThumbnailProviderContext context) {
                if (!string.Equals(ThumbnailPathTools.NormalizeFormat(request.Format), "SVG", StringComparison.OrdinalIgnoreCase)) {
                    return ThumbnailResult.Empty();
                }

                return ThumbnailRenderingTools.GetOrCreateCachedThumbnail(
                    identity,
                    context.Cache,
                    ThumbnailResultSource.Svg,
                    () => SvgThumbnailHelper.RenderSvg(identity.FullPath, request.TargetPixels));
            }
        }

        private sealed class ShellThumbnailProvider : IThumbnailProvider {
            public ThumbnailResult TryGetThumbnail(ThumbnailRequest request, ThumbnailPathIdentity identity, ThumbnailProviderContext context) {
                return ThumbnailRenderingTools.GetOrCreateCachedThumbnail(
                    identity,
                    context.Cache,
                    ThumbnailResultSource.Shell,
                    () => LoadShellThumbnail(identity.FullPath));
            }

            private static BitmapSource LoadShellThumbnail(string fullPath) {
                try {
                    using (ShellObject shellUnit = ShellObject.FromParsingName(fullPath)) {
                        BitmapSource thumbnail = shellUnit.Thumbnail.LargeBitmapSource;
                        if (thumbnail != null && thumbnail.CanFreeze) {
                            thumbnail.Freeze();
                        }
                        return thumbnail;
                    }
                } catch (Exception ex) {
                    Trace.WriteLine($"{fullPath} {ex.Message}");
                    return null;
                }
            }
        }

        private sealed class DefaultFileIconThumbnailProvider : IThumbnailProvider {
            public ThumbnailResult TryGetThumbnail(ThumbnailRequest request, ThumbnailPathIdentity identity, ThumbnailProviderContext context) {
                return ThumbnailResult.Fallback(StaticIconCatalog.GenericFileIcon, ThumbnailResultSource.GenericIcon, identity.CacheKey, "No rich thumbnail provider returned an image.");
            }
        }
    }

    internal static class StaticIconCatalog {
        private static readonly Lazy<BitmapSource> FolderIcon = new Lazy<BitmapSource>(() => ThumbnailRenderingTools.LoadPackBitmap("ico/dif.png", 210));
        private static readonly Lazy<BitmapSource> ArchiveIcon = new Lazy<BitmapSource>(() => ThumbnailRenderingTools.LoadPackBitmap("ico/open-7z-file.png", 210));
        private static readonly Lazy<BitmapSource> BinaryIcon = new Lazy<BitmapSource>(() => ThumbnailRenderingTools.LoadPackBitmap("ico/binary.png", 210));
        private static readonly Lazy<BitmapSource> IniIcon = new Lazy<BitmapSource>(() => ThumbnailRenderingTools.LoadPackBitmap("ico/ini.png", 100));
        private static readonly Lazy<BitmapSource> GenericFileIconLazy = new Lazy<BitmapSource>(() => ThumbnailRenderingTools.LoadPackBitmap("ico/ff.png", 210));

        private static readonly HashSet<string> ArchiveFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "ZIP", "7Z", "RAR", "TAR", "GZ"
        };

        private static readonly HashSet<string> IniFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "INI", "TBL"
        };

        private static readonly HashSet<string> BinaryFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "DLL", "BIN", "DAT", "BBL", "PFS", "EXE", "SYS"
        };

        public static BitmapSource Folder => FolderIcon.Value;
        public static BitmapSource GenericFileIcon => GenericFileIconLazy.Value;

        public static ThumbnailResult TryGetStaticIcon(string format, bool isDirectory, string cacheKey) {
            if (isDirectory) {
                return ThumbnailResult.Fallback(FolderIcon.Value, ThumbnailResultSource.GenericIcon, cacheKey);
            }

            string normalizedFormat = ThumbnailPathTools.NormalizeFormat(format);
            if (ArchiveFormats.Contains(normalizedFormat)) {
                return ThumbnailResult.Fallback(ArchiveIcon.Value, ThumbnailResultSource.GenericIcon, cacheKey);
            }

            if (IniFormats.Contains(normalizedFormat)) {
                return ThumbnailResult.Fallback(IniIcon.Value, ThumbnailResultSource.GenericIcon, cacheKey);
            }

            if (BinaryFormats.Contains(normalizedFormat)) {
                return ThumbnailResult.Fallback(BinaryIcon.Value, ThumbnailResultSource.GenericIcon, cacheKey);
            }

            return null;
        }
    }

    internal static class ThumbnailRenderingTools {
        public static ThumbnailResult GetOrCreateCachedThumbnail(
            ThumbnailPathIdentity identity,
            LruBitmapCache cache,
            ThumbnailResultSource source,
            Func<BitmapSource> createThumbnail) {
            if (identity == null || string.IsNullOrWhiteSpace(identity.CacheKey)) {
                return ThumbnailResult.Empty("Empty cache identity.");
            }

            if (cache.TryGet(identity.CacheKey, out BitmapSource cachedThumb)) {
                return ThumbnailResult.Ready(cachedThumb, ThumbnailResultSource.MemoryCache, identity.CacheKey);
            }

            BitmapSource thumbnail = createThumbnail();
            if (thumbnail == null) {
                return ThumbnailResult.Empty();
            }

            if (thumbnail.CanFreeze) {
                thumbnail.Freeze();
            }

            cache.AddOrUpdate(identity.CacheKey, thumbnail);
            return ThumbnailResult.Ready(thumbnail, source, identity.CacheKey);
        }

        public static BitmapSource LoadPackBitmap(string relativePath, int decodeWidth) {
            try {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = decodeWidth;
                bitmap.UriSource = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            } catch (Exception ex) {
                Trace.WriteLine($"LoadPackBitmap failed for {relativePath}: {ex.Message}");
                return null;
            }
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }

    internal sealed class LruBitmapCache {
        private sealed class CacheEntry {
            public CacheEntry(BitmapSource image, long sizeBytes, LinkedListNode<string> node) {
                Image = image;
                SizeBytes = sizeBytes;
                Node = node;
            }

            public BitmapSource Image { get; }
            public long SizeBytes { get; }
            public LinkedListNode<string> Node { get; }
        }

        private readonly long capacityBytes;
        private readonly Dictionary<string, CacheEntry> entries;
        private readonly LinkedList<string> lru;
        private readonly object sync = new object();
        private long currentBytes;

        public LruBitmapCache(long capacityBytes, IEqualityComparer<string> comparer) {
            this.capacityBytes = capacityBytes;
            entries = new Dictionary<string, CacheEntry>(comparer);
            lru = new LinkedList<string>();
        }

        public bool TryGet(string key, out BitmapSource image) {
            lock (sync) {
                if (entries.TryGetValue(key, out CacheEntry entry)) {
                    lru.Remove(entry.Node);
                    lru.AddFirst(entry.Node);
                    image = entry.Image;
                    return true;
                }
            }

            image = null;
            return false;
        }

        public void AddOrUpdate(string key, BitmapSource image) {
            if (string.IsNullOrWhiteSpace(key) || image == null) return;

            long sizeBytes = EstimateBitmapBytes(image);
            lock (sync) {
                if (entries.TryGetValue(key, out CacheEntry existing)) {
                    currentBytes -= existing.SizeBytes;
                    lru.Remove(existing.Node);
                    entries.Remove(key);
                }

                var node = new LinkedListNode<string>(key);
                lru.AddFirst(node);
                entries[key] = new CacheEntry(image, sizeBytes, node);
                currentBytes += sizeBytes;
                EvictIfNeeded();
            }
        }

        private void EvictIfNeeded() {
            while (currentBytes > capacityBytes && lru.Last != null) {
                string key = lru.Last.Value;
                if (entries.TryGetValue(key, out CacheEntry entry)) {
                    currentBytes -= entry.SizeBytes;
                    entries.Remove(key);
                }
                lru.RemoveLast();
            }
        }

        private static long EstimateBitmapBytes(BitmapSource image) {
            int bitsPerPixel = image?.Format.BitsPerPixel ?? 0;
            if (bitsPerPixel <= 0) {
                bitsPerPixel = 32;
            }
            long pixels = (long)image.PixelWidth * image.PixelHeight;
            return pixels * bitsPerPixel / 8;
        }
    }

    public static class ThumbnailPathTools {
        public static ThumbnailPathIdentity CreateIdentity(string fullPath) {
            string normalizedPath = NormalizeFilePath(fullPath);
            return new ThumbnailPathIdentity(normalizedPath, BuildCacheKey(normalizedPath));
        }

        public static string BuildCacheKey(string fullPath) {
            string normalizedPath = NormalizeFilePath(fullPath);
            if (string.IsNullOrWhiteSpace(normalizedPath)) {
                return string.Empty;
            }

            try {
                var fileInfo = new FileInfo(normalizedPath);
                if (!fileInfo.Exists) {
                    return normalizedPath;
                }

                return string.Join("|",
                    normalizedPath,
                    fileInfo.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                    fileInfo.Length.ToString(CultureInfo.InvariantCulture));
            } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException) {
                Trace.WriteLine($"{normalizedPath} {ex.Message}");
                return normalizedPath;
            }
        }

        public static string NormalizeFilePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            try {
                return Path.GetFullPath(path.Trim());
            } catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is NotSupportedException || ex is UnauthorizedAccessException) {
                Trace.WriteLine($"{path} {ex.Message}");
                return path.Trim();
            }
        }

        public static string NormalizeFormat(string format) {
            return (format ?? string.Empty).Trim().TrimStart('.').ToUpperInvariant();
        }
    }
}
