using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinFinder.Backend;

namespace WinFinder.Utilities {
    internal static class PlaceholderIconFactory {
        private const int PixelSize = 210;
        private const int MaxCachedBadges = 64;
        private const int PrewarmBatchSize = 4;
        private static readonly Dictionary<string, BitmapSource> BadgeCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<string> BadgeInsertionOrder = new Queue<string>();
        private static readonly object BadgeSync = new object();
        private static bool prewarmScheduled;

        private static readonly string[] PrewarmExtensions = {
            "JPG", "JPEG", "PNG", "GIF", "BMP", "WEBP", "HEIC",
            "MP4", "MKV", "AVI", "MOV",
            "MP3", "WAV", "FLAC",
            "PDF", "DOC", "DOCX", "XLS", "XLSX", "PPT", "PPTX", "TXT", "MD", "CSV", "LOG",
            "JSON", "XML", "HTML", "CSS", "JS", "TS", "CS", "PY", "C", "CPP", "H"
        };

        public static BitmapSource GetPlaceholder(string format, bool isDirectory) {
            ThumbnailResult known = StaticIconCatalog.TryGetStaticIcon(format, isDirectory, string.Empty);
            if (known != null && known.HasImage) {
                return known.Image;
            }

            string extension = NormalizeExtension(format);
            if (extension.Length == 0) {
                return StaticIconCatalog.GenericFileIcon;
            }

            SchedulePrewarm();
            return GetOrCreateBadgeIcon(extension);
        }

        private static string NormalizeExtension(string format) {
            string extension = (format ?? string.Empty).Trim().TrimStart('.');
            return extension.Length > 8 ? extension.Substring(0, 8) : extension;
        }

        private static BitmapSource GetOrCreateBadgeIcon(string extension) {
            lock (BadgeSync) {
                if (BadgeCache.TryGetValue(extension, out BitmapSource cached)) {
                    return cached;
                }
            }

            BitmapSource created = CreateBadgeIcon(extension);

            lock (BadgeSync) {
                if (BadgeCache.TryGetValue(extension, out BitmapSource raced)) {
                    return raced;
                }

                EvictOldestBadges();
                BadgeCache[extension] = created;
                BadgeInsertionOrder.Enqueue(extension);
            }

            return created;
        }

        private static void EvictOldestBadges() {
            if (BadgeCache.Count < MaxCachedBadges) {
                return;
            }

            int evictCount = Math.Max(1, MaxCachedBadges / 2);
            for (int i = 0; i < evictCount && BadgeInsertionOrder.Count > 0; i++) {
                BadgeCache.Remove(BadgeInsertionOrder.Dequeue());
            }
        }

        private static void SchedulePrewarm() {
            if (prewarmScheduled) {
                return;
            }

            prewarmScheduled = true;
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) {
                return;
            }

            var remaining = new Queue<string>(PrewarmExtensions);
            Action step = null;
            step = () => {
                for (int i = 0; i < PrewarmBatchSize && remaining.Count > 0; i++) {
                    GetOrCreateBadgeIcon(remaining.Dequeue());
                }

                Dispatcher current = Application.Current?.Dispatcher;
                if (remaining.Count > 0 && current != null && !current.HasShutdownStarted && !current.HasShutdownFinished) {
                    current.BeginInvoke(DispatcherPriority.Background, step);
                }
            };

            dispatcher.BeginInvoke(DispatcherPriority.Background, step);
        }

        private static BitmapSource CreateBadgeIcon(string extension) {
            const double size = PixelSize;
            Color accent = GetCategoryColor(extension);
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen()) {
                var page = new Rect(size * 0.14, size * 0.06, size * 0.72, size * 0.88);
                double fold = size * 0.18;
                double bandHeight = page.Height * 0.24;
                var band = new Rect(page.Left, page.Bottom - bandHeight, page.Width, bandHeight);

                var paperBrush = new LinearGradientBrush(
                    Color.FromRgb(0xFD, 0xFD, 0xFE),
                    Color.FromRgb(0xEE, 0xEF, 0xF2),
                    new Point(0.5, 0.0),
                    new Point(0.5, 1.0));
                paperBrush.Freeze();

                var accentBrush = new SolidColorBrush(accent);
                accentBrush.Freeze();

                var outlinePen = new Pen(new SolidColorBrush(Color.FromRgb(0xC6, 0xC7, 0xCC)), Math.Max(1.0, size * 0.008));
                outlinePen.Freeze();

                context.DrawRectangle(paperBrush, null, page);
                context.DrawRectangle(accentBrush, null, band);

                var foldGeometry = new StreamGeometry();
                using (StreamGeometryContext geometry = foldGeometry.Open()) {
                    geometry.BeginFigure(new Point(page.Right - fold, page.Top), true, true);
                    geometry.LineTo(new Point(page.Right, page.Top), true, false);
                    geometry.LineTo(new Point(page.Right, page.Top + fold), true, false);
                }
                foldGeometry.Freeze();
                context.DrawGeometry(accentBrush, null, foldGeometry);

                context.DrawRectangle(null, outlinePen, page);

                string text = extension.ToUpperInvariant();
                var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                double fontSize = bandHeight * 0.56;
                double maxWidth = band.Width * 0.88;
                FormattedText formatted = CreateBadgeText(text, typeface, fontSize);
                while (formatted.Width > maxWidth && fontSize > bandHeight * 0.3) {
                    fontSize *= 0.88;
                    formatted = CreateBadgeText(text, typeface, fontSize);
                }

                if (formatted.Width > maxWidth) {
                    formatted.MaxTextWidth = maxWidth;
                    formatted.Trimming = TextTrimming.CharacterEllipsis;
                }

                double textWidth = Math.Min(formatted.Width, maxWidth);
                double textX = page.Left + (page.Width - textWidth) / 2.0;
                double textY = band.Top + (band.Height - formatted.Height) / 2.0;
                context.DrawText(formatted, new Point(textX, textY));
            }

            var bitmap = new RenderTargetBitmap(PixelSize, PixelSize, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static FormattedText CreateBadgeText(string text, Typeface typeface, double fontSize) {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.White,
                1.0);
        }

        private static Color GetCategoryColor(string extension) {
            return ExtensionCategoryColors.TryGetValue(extension, out Color color)
                ? color
                : Color.FromRgb(0x6B, 0x72, 0x80);
        }

        private static readonly Dictionary<string, Color> ExtensionCategoryColors = BuildCategoryColorMap();

        private static Dictionary<string, Color> BuildCategoryColorMap() {
            var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            AddCategory(map, Color.FromRgb(0x33, 0xA0, 0x6F),
                "JPG", "JPEG", "JPE", "JFIF", "PNG", "GIF", "BMP", "WEBP", "TIF", "TIFF",
                "HEIC", "HEIF", "AVIF", "JXL", "ICO", "SVG", "RAW", "CR2", "NEF", "ARW", "DNG", "PSD", "PSB", "XCF");
            AddCategory(map, Color.FromRgb(0x7A, 0x5A, 0xF8),
                "MP4", "M4V", "MOV", "MKV", "AVI", "WEBM", "WMV", "MPEG", "MPG", "TS", "M2TS", "3GP", "FLV", "RM", "RMVB", "VOB");
            AddCategory(map, Color.FromRgb(0xD6, 0x40, 0x7F),
                "MP3", "WAV", "FLAC", "AAC", "OGG", "M4A", "WMA", "APE", "OPUS", "MID", "MIDI", "AIFF", "AMR");
            AddCategory(map, Color.FromRgb(0xE0, 0x8A, 0x00),
                "ZIP", "7Z", "RAR", "TAR", "GZ", "BZ2", "XZ", "TGZ", "ZST", "LZ", "LZMA", "ISO", "CAB");
            AddCategory(map, Color.FromRgb(0xD9, 0x53, 0x4F),
                "PDF", "DOC", "DOCX", "XLS", "XLSX", "PPT", "PPTX", "RTF", "ODT", "ODS", "ODP", "TXT", "MD", "CSV", "LOG", "WPS", "ET");
            AddCategory(map, Color.FromRgb(0x4F, 0x7E, 0xEA),
                "JSON", "XML", "HTML", "HTM", "XHTML", "CSS", "JS", "MJS", "TSX", "JSX", "CS", "PY", "C", "CPP", "CC", "H", "HPP",
                "JAVA", "GO", "RS", "RB", "PHP", "SQL", "SH", "BAT", "CMD", "PS1", "YAML", "YML", "TOML", "INI", "CONFIG", "SLN",
                "CSPROJ", "VB", "LUA", "KT", "SWIFT");
            AddCategory(map, Color.FromRgb(0x5B, 0x64, 0x72),
                "EXE", "MSI", "DLL", "SYS", "BIN", "DAT", "COM", "SCR", "OCX", "LIB", "OBJ", "PDB");
            return map;
        }

        private static void AddCategory(Dictionary<string, Color> map, Color color, params string[] extensions) {
            foreach (string extension in extensions) {
                map[extension] = color;
            }
        }
    }
}
