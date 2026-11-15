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
            Run("FileOperations copy, move, rename, create, and paste stay inside the sandbox", FileOperationsWorkInsideSandbox);
            Run("FileOperations refuses work outside the sandbox and will not delete the sandbox root", FileOperationsEnforcesSandboxGuard);
            Run("SessionStore round-trips tabs, view mode, and layout in the sandbox", SessionStoreRoundTripsInSandbox);
            Run("SessionStore drops missing folders, clamps the active tab, and recovers from bad files", SessionStoreNormalizesAndRecovers);
            Run("SessionStore formats tab titles from folder names", SessionStoreFormatsTitles);

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



        private static void SessionStoreRoundTripsInSandbox() {
            string sandbox = CreateSandbox();
            try {
                string folderA = Path.Combine(sandbox, "alpha");
                string folderB = Path.Combine(sandbox, "beta");
                Directory.CreateDirectory(folderA);
                Directory.CreateDirectory(folderB);
                string file = Path.Combine(sandbox, "session.json");

                var state = new WindowSessionState {
                    Version = SessionStore.CurrentVersion,
                    DualPane = true,
                    Stacked = true,
                    Pane1 = new PaneSessionState {
                        ActiveTabIndex = 1,
                        Tabs = new List<WorkspaceTabState> {
                            new WorkspaceTabState { Path = folderA, View = "list" },
                            new WorkspaceTabState { Path = folderB, View = "grid" }
                        }
                    },
                    Pane2 = new PaneSessionState {
                        ActiveTabIndex = 0,
                        Tabs = new List<WorkspaceTabState> {
                            new WorkspaceTabState { Path = folderA, View = "list" }
                        }
                    }
                };

                SessionStore.Save(state, file);
                AssertTrue(File.Exists(file), "Session file was not written inside the sandbox.");
                AssertTrue(FileOperations.IsSubpathOf(file, sandbox), "Session file left the sandbox.");

                WindowSessionState loaded = SessionStore.Load(file, sandbox);
                AssertTrue(loaded.DualPane, "Dual-pane flag was not restored.");
                AssertTrue(loaded.Stacked, "Stacked layout flag was not restored.");
                AssertEqual(2, loaded.Pane1.Tabs.Count, "Pane1 tab count mismatch.");
                AssertEqual(1, loaded.Pane1.ActiveTabIndex, "Pane1 active tab was not restored.");
                AssertEqual(Path.GetFullPath(folderB), loaded.Pane1.Tabs[1].Path, "Pane1 tab path mismatch.");
                AssertEqual("grid", loaded.Pane1.Tabs[1].View, "Grid view was not restored.");
                AssertEqual(Path.GetFullPath(folderA), loaded.Pane2.Tabs[0].Path, "Pane2 tab path mismatch.");
            } finally {
                DeleteSandbox(sandbox);
            }
        }

        private static void SessionStoreNormalizesAndRecovers() {
            string sandbox = CreateSandbox();
            try {
                string exists = Path.Combine(sandbox, "keeps");
                Directory.CreateDirectory(exists);
                string missing = Path.Combine(sandbox, "gone");
                string file = Path.Combine(sandbox, "session.json");

                var state = new WindowSessionState {
                    Pane1 = new PaneSessionState {
                        ActiveTabIndex = 40,
                        Tabs = new List<WorkspaceTabState> {
                            new WorkspaceTabState { Path = missing, View = "grid" },
                            new WorkspaceTabState { Path = exists, View = "GRID" },
                            new WorkspaceTabState { Path = "   ", View = "list" }
                        }
                    }
                };
                SessionStore.Save(state, file);

                WindowSessionState loaded = SessionStore.Load(file, exists);
                AssertEqual(1, loaded.Pane1.Tabs.Count, "Missing folders should be dropped on load.");
                AssertEqual(0, loaded.Pane1.ActiveTabIndex, "Active tab index should be clamped.");
                AssertEqual("grid", loaded.Pane1.Tabs[0].View, "View mode should normalize to grid.");
                AssertTrue(loaded.Pane2.Tabs.Count == 1, "Pane2 should fall back to a default tab.");

                File.WriteAllText(file, "{ this is not json");
                WindowSessionState recovered = SessionStore.Load(file, exists);
                AssertEqual(Path.GetFullPath(exists), recovered.Pane1.Tabs[0].Path, "Corrupt session should fall back to the default path.");

                string missingFile = Path.Combine(sandbox, "no-such-session.json");
                WindowSessionState created = SessionStore.Load(missingFile, exists);
                AssertEqual(Path.GetFullPath(exists), created.Pane1.Tabs[0].Path, "Missing session file should use the default path.");
            } finally {
                DeleteSandbox(sandbox);
            }
        }

        private static void SessionStoreFormatsTitles() {
            AssertEqual("Documents", SessionStore.FormatTabTitle(@"C:\Users\demo\Documents"), "Folder name should be the tab title.");
            AssertEqual(@"D:\", SessionStore.FormatTabTitle(@"D:\"), "Drive roots should keep a readable title.");
            string emptyTitle = SessionStore.FormatTabTitle("");
            AssertTrue(emptyTitle == "新标签页" || emptyTitle == "New Tab", "Empty paths should use the new-tab title.");
        }

        private static void FileOperationsWorkInsideSandbox() {
            string sandbox = CreateSandbox();
            try {
                string fileA = Path.Combine(sandbox, "a.txt");
                File.WriteAllText(fileA, "hello");
                string folder = Path.Combine(sandbox, "sub");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "inner.txt"), "inner");

                FileOperationResult copy = FileOperations.Copy(new[] { fileA }, sandbox, sandbox);
                AssertTrue(copy.Success, copy.Error);
                AssertTrue(File.Exists(Path.Combine(sandbox, "a (1).txt")), "Unique copy was not created.");

                FileOperationResult rename = FileOperations.Rename(fileA, "renamed.txt", sandbox);
                AssertTrue(rename.Success, rename.Error);
                AssertTrue(File.Exists(Path.Combine(sandbox, "renamed.txt")), "Renamed file is missing.");
                AssertTrue(!File.Exists(fileA), "Original file still exists after rename.");

                FileOperationResult createdFolder = FileOperations.CreateFolder(sandbox, "New Folder", sandbox);
                AssertTrue(createdFolder.Success, createdFolder.Error);
                AssertTrue(Directory.Exists(createdFolder.AffectedPaths[0]), "Created folder is missing.");

                FileOperationResult createdFile = FileOperations.CreateFile(sandbox, "New File.txt", sandbox);
                AssertTrue(createdFile.Success, createdFile.Error);
                AssertTrue(File.Exists(createdFile.AffectedPaths[0]), "Created file is missing.");

                FileOperations.SetClipboard(new[] { Path.Combine(sandbox, "renamed.txt") }, FileTransferMode.Copy);
                FileOperationResult paste = FileOperations.Paste(folder, sandbox);
                AssertTrue(paste.Success, paste.Error);
                AssertTrue(File.Exists(Path.Combine(folder, "renamed.txt")), "Pasted copy is missing.");

                string destDir = Path.Combine(sandbox, "dest");
                Directory.CreateDirectory(destDir);
                FileOperationResult move = FileOperations.Move(new[] { Path.Combine(sandbox, "renamed.txt") }, destDir, sandbox);
                AssertTrue(move.Success, move.Error);
                AssertTrue(!File.Exists(Path.Combine(sandbox, "renamed.txt")), "Moved source still exists.");
                AssertTrue(File.Exists(Path.Combine(destDir, "renamed.txt")), "Moved file is missing.");

                FileOperations.SetClipboard(new[] { Path.Combine(destDir, "renamed.txt") }, FileTransferMode.Move);
                FileOperationResult cutPaste = FileOperations.Paste(sandbox, sandbox);
                AssertTrue(cutPaste.Success, cutPaste.Error);
                AssertTrue(File.Exists(Path.Combine(sandbox, "renamed.txt")), "Cut-paste destination is missing.");
                AssertTrue(!File.Exists(Path.Combine(destDir, "renamed.txt")), "Cut-paste source still exists.");
                AssertTrue(FileOperations.ClipboardState.IsEmpty, "Clipboard should clear after a move paste.");

                FileOperationResult deleted = FileOperations.Delete(new[] { Path.Combine(sandbox, "renamed.txt"), folder }, false, sandbox);
                AssertTrue(deleted.Success, deleted.Error);
                AssertTrue(!File.Exists(Path.Combine(sandbox, "renamed.txt")), "Deleted file still exists.");
                AssertTrue(!Directory.Exists(folder), "Deleted folder still exists.");
                AssertTrue(FileOperations.IsSubpathOf(sandbox, sandbox), "Sandbox should be a subpath of itself.");
                AssertTrue(!FileOperations.IsSubpathOf(Path.GetTempPath(), sandbox), "Temp path must not count as inside the sandbox.");
            } finally {
                DeleteSandbox(sandbox);
            }
        }

        private static void FileOperationsEnforcesSandboxGuard() {
            string sandbox = CreateSandbox();
            string outside = Path.Combine(Path.GetTempPath(), "WinFinder.Tests.Outside." + Guid.NewGuid().ToString("N") + ".txt");
            try {
                File.WriteAllText(Path.Combine(sandbox, "inside.txt"), "safe");
                File.WriteAllText(outside, "do-not-touch");

                FileOperationResult refusedCopy = FileOperations.Copy(new[] { Path.Combine(sandbox, "inside.txt") }, Path.GetDirectoryName(outside), sandbox);
                AssertTrue(!refusedCopy.Success, "Copying out of the sandbox should fail.");
                AssertTrue(!File.Exists(Path.Combine(Path.GetDirectoryName(outside), "inside.txt")), "A file was created outside the sandbox.");

                FileOperationResult refusedDelete = FileOperations.Delete(new[] { outside }, false, sandbox);
                AssertTrue(!refusedDelete.Success, "Deleting outside the sandbox should fail.");
                AssertTrue(File.Exists(outside), "A file outside the sandbox was deleted.");

                FileOperationResult refusedRoot = FileOperations.Delete(new[] { sandbox }, false, sandbox);
                AssertTrue(!refusedRoot.Success, "Deleting the sandbox root should fail.");
                AssertTrue(Directory.Exists(sandbox), "The sandbox root was deleted.");
            } finally {
                TryDelete(outside);
                DeleteSandbox(sandbox);
            }
        }

        private static string CreateSandbox() {
            string sandbox = Path.Combine(Path.GetTempPath(), "WinFinder.TestSandbox", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sandbox);
            if (!FileOperations.IsSubpathOf(sandbox, Path.Combine(Path.GetTempPath(), "WinFinder.TestSandbox"))) {
                throw new InvalidOperationException("Sandbox path was not created under the expected root.");
            }
            return sandbox;
        }

        private static void DeleteSandbox(string sandbox) {
            try {
                if (!string.IsNullOrWhiteSpace(sandbox) && Directory.Exists(sandbox)) {
                    string root = Path.Combine(Path.GetTempPath(), "WinFinder.TestSandbox");
                    if (FileOperations.IsSubpathOf(sandbox, root)) {
                        Directory.Delete(sandbox, true);
                    }
                }
            } catch {
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
