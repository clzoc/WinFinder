using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using WinFinder;
using WinFinder.Backend;

namespace WinFinder.Tests {
    internal static class Program {
        private static readonly List<string> Failures = new List<string>();

        private static int Main() {
            Run("NaturalStringComparer sorts numeric file names naturally", NaturalStringComparerSortsNumericNames);
            Run("NaturalStringComparer handles null and property sorting", NaturalStringComparerHandlesNullAndPropertySorting);
            Run("ExplorerQuery normalizes nulls and negative paging", ExplorerQueryNormalizesInputs);
            Run("ExplorerItem maps files, directories, roots, and hidden visibility", ExplorerItemMapsPathsAndVisibility);
            Run("ThumbnailPathTools normalizes paths and formats", ThumbnailPathToolsNormalizesInputs);
            Run("ThumbnailPathTools cache key tracks file identity", ThumbnailPathToolsCacheKeyTracksFileIdentity);
            Run("ThumbnailPathTools returns normalized path for missing file", ThumbnailPathToolsUsesPathForMissingFile);

            if (Failures.Count == 0) {
                Console.WriteLine("All WinFinder.Tests passed.");
                return 0;
            }

            Console.Error.WriteLine($"{Failures.Count} WinFinder.Tests failed:");
            foreach (string failure in Failures) {
                Console.Error.WriteLine(failure);
            }
            return 1;
        }

        private static void Run(string name, Action test) {
            try {
                test();
                Console.WriteLine($"PASS {name}");
            } catch (Exception ex) {
                Failures.Add($"FAIL {name}: {ex.Message}");
            }
        }

        private static void NaturalStringComparerSortsNumericNames() {
            var values = new[] { "file10.txt", "file2.txt", "file1.txt", "file12.txt" };
            Array.Sort(values, new NaturalStringComparer());
            AssertSequence(new[] { "file1.txt", "file2.txt", "file10.txt", "file12.txt" }, values);
        }

        private static void NaturalStringComparerHandlesNullAndPropertySorting() {
            var comparer = new NaturalStringComparer();
            AssertTrue(comparer.Compare(null, null) == 0, "null should equal null.");
            AssertTrue(comparer.Compare(null, "a") < 0, "null should sort before non-null.");
            AssertTrue(comparer.Compare("a", null) > 0, "non-null should sort after null.");
            AssertTrue(comparer.Compare("File2", "file10") < 0, "comparison should ignore case and sort numbers naturally.");

            var items = new ArrayList {
                new SortProbe { Name = "item10" },
                new SortProbe { Name = "item2" },
                new SortProbe { Name = "item1" }
            };
            items.Sort(NaturalStringComparer.CreateComparerForProperty(nameof(SortProbe.Name), ListSortDirection.Ascending));
            AssertSequence(new[] { "item1", "item2", "item10" }, items.Cast<SortProbe>().Select(item => item.Name).ToArray());

            items.Sort(NaturalStringComparer.CreateComparerForProperty(nameof(SortProbe.Name), ListSortDirection.Descending));
            AssertSequence(new[] { "item10", "item2", "item1" }, items.Cast<SortProbe>().Select(item => item.Name).ToArray());
        }

        private static void ExplorerQueryNormalizesInputs() {
            ExplorerQuery browse = ExplorerQuery.Browse(null, -10, -20);
            AssertEqual(ExplorerQueryKind.Browse, browse.Kind, "Browse kind mismatch.");
            AssertEqual(string.Empty, browse.Path, "Browse path should default to empty.");
            AssertEqual(0, browse.Offset, "Browse offset should be clamped.");
            AssertEqual(0, browse.MaxResults, "Browse max results should be clamped.");

            ExplorerQuery search = ExplorerQuery.Search(null, -1, 25);
            AssertEqual(ExplorerQueryKind.Search, search.Kind, "Search kind mismatch.");
            AssertEqual(string.Empty, search.Text, "Search text should default to empty.");
            AssertEqual(0, search.Offset, "Search offset should be clamped.");
            AssertEqual(25, search.MaxResults, "Search max results should be preserved.");
        }

        private static void ExplorerItemMapsPathsAndVisibility() {
            string root = Path.GetPathRoot(Environment.SystemDirectory);
            var rootItem = new ExplorerItem(root, true, null, null, FileAttributes.Directory);
            AssertEqual(root, rootItem.Name, "Root item should use full path as display name.");
            AssertEqual(string.Empty, rootItem.Extension, "Directories should not have extensions.");
            AssertTrue(rootItem.IsVisible, "Plain directory should be visible.");

            string filePath = Path.Combine(root, "Folder", "Example.Report.PDF");
            var fileItem = new ExplorerItem(filePath, false, 123, new DateTime(2024, 1, 2), FileAttributes.Archive);
            AssertEqual("Example.Report.PDF", fileItem.Name, "File name mapping mismatch.");
            AssertEqual("PDF", fileItem.Extension, "File extension should be uppercase without dot.");
            AssertEqual(123L, fileItem.SizeBytes.Value, "File size mismatch.");
            AssertTrue(fileItem.IsVisible, "Archive file should be visible.");

            var hiddenItem = new ExplorerItem(filePath, false, 123, null, FileAttributes.Hidden);
            AssertTrue(!hiddenItem.IsVisible, "Hidden file should not be visible.");

            var systemItem = new ExplorerItem(filePath, false, 123, null, FileAttributes.System);
            AssertTrue(!systemItem.IsVisible, "System file should not be visible.");
        }

        private static void ThumbnailPathToolsNormalizesInputs() {
            AssertEqual(string.Empty, ThumbnailPathTools.NormalizeFilePath(null), "Null path should normalize to empty.");
            AssertEqual(string.Empty, ThumbnailPathTools.NormalizeFilePath("   "), "Whitespace path should normalize to empty.");
            AssertEqual("PDF", ThumbnailPathTools.NormalizeFormat(".pdf"), "Format should trim dot and uppercase.");
            AssertEqual("TXT", ThumbnailPathTools.NormalizeFormat(" txt "), "Format should trim whitespace and uppercase.");

            string expected = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string actual = ThumbnailPathTools.NormalizeFilePath(".");
            AssertEqual(expected, actual.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "Relative path should be made absolute.");
        }

        private static void ThumbnailPathToolsCacheKeyTracksFileIdentity() {
            string path = Path.Combine(Path.GetTempPath(), "WinFinder.Tests." + Guid.NewGuid().ToString("N") + ".txt");
            try {
                File.WriteAllText(path, "one");
                string normalized = ThumbnailPathTools.NormalizeFilePath(path);
                string first = ThumbnailPathTools.BuildCacheKey(path);
                AssertTrue(first.StartsWith(normalized + "|", StringComparison.OrdinalIgnoreCase), "Existing file key should include normalized path and metadata.");

                File.WriteAllText(path, "one plus more text");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
                string second = ThumbnailPathTools.BuildCacheKey(path);
                AssertTrue(!string.Equals(first, second, StringComparison.Ordinal), "Cache key should change when file identity changes.");

                ThumbnailPathIdentity identity = ThumbnailPathTools.CreateIdentity(path);
                AssertEqual(normalized, identity.FullPath, "Identity full path mismatch.");
                AssertEqual(second, identity.CacheKey, "Identity cache key mismatch.");
            } finally {
                TryDelete(path);
            }
        }

        private static void ThumbnailPathToolsUsesPathForMissingFile() {
            string path = Path.Combine(Path.GetTempPath(), "WinFinder.Tests.Missing." + Guid.NewGuid().ToString("N") + ".txt");
            string normalized = ThumbnailPathTools.NormalizeFilePath(path);
            AssertEqual(normalized, ThumbnailPathTools.BuildCacheKey(path), "Missing file key should be the normalized path.");
        }

        private static void TryDelete(string path) {
            try {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
                    File.Delete(path);
                }
            } catch {
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message) {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
                throw new InvalidOperationException($"{message} Expected '{expected}', actual '{actual}'.");
            }
        }

        private static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual) {
            if (expected.Count != actual.Count) {
                throw new InvalidOperationException($"Sequence length mismatch. Expected {expected.Count}, actual {actual.Count}.");
            }

            for (int i = 0; i < expected.Count; i++) {
                if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i])) {
                    throw new InvalidOperationException($"Sequence mismatch at {i}. Expected '{expected[i]}', actual '{actual[i]}'.");
                }
            }
        }

        private static void AssertTrue(bool condition, string message) {
            if (!condition) {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class SortProbe {
            public string Name { get; set; }
        }
    }
}
