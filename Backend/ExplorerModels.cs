using System;
using System.IO;

namespace WinFinder.Backend {
    public enum ExplorerQueryKind {
        Browse,
        Search
    }

    public enum ExplorerActionKind {
        Open,
        OpenContainingFolder,
        CopyPath
    }

    public sealed class ExplorerQuery {
        private ExplorerQuery(ExplorerQueryKind kind, string path, string text, int offset, int maxResults) {
            Kind = kind;
            Path = path ?? string.Empty;
            Text = text ?? string.Empty;
            Offset = Math.Max(0, offset);
            MaxResults = Math.Max(0, maxResults);
        }

        public ExplorerQueryKind Kind { get; }
        public string Path { get; }
        public string Text { get; }
        public int Offset { get; }
        public int MaxResults { get; }

        public static ExplorerQuery Browse(string path, int offset = 0, int maxResults = 10000) {
            return new ExplorerQuery(ExplorerQueryKind.Browse, path, string.Empty, offset, maxResults);
        }

        public static ExplorerQuery Search(string text, int offset = 0, int maxResults = 200) {
            return new ExplorerQuery(ExplorerQueryKind.Search, string.Empty, text, offset, maxResults);
        }
    }

    public sealed class ExplorerItem {
        public ExplorerItem(string fullPath, bool isDirectory, long? sizeBytes, DateTime? modified, FileAttributes attributes) {
            FullPath = fullPath ?? string.Empty;
            IsDirectory = isDirectory;
            SizeBytes = sizeBytes;
            Modified = modified;
            Attributes = attributes;

            string name = Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(name)) {
                name = FullPath;
            }

            Name = name ?? string.Empty;
            Extension = IsDirectory
                ? string.Empty
                : Path.GetExtension(FullPath).TrimStart('.').ToUpperInvariant();
        }

        public string FullPath { get; }
        public string Name { get; }
        public bool IsDirectory { get; }
        public long? SizeBytes { get; }
        public DateTime? Modified { get; }
        public FileAttributes Attributes { get; }
        public string Extension { get; }

        public bool IsVisible => (Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
    }

    public sealed class ExplorerActionRequest {
        public ExplorerActionRequest(ExplorerActionKind kind, string path) {
            Kind = kind;
            Path = path ?? string.Empty;
        }

        public ExplorerActionKind Kind { get; }
        public string Path { get; }
    }

    public sealed class ExplorerActionResult {
        private ExplorerActionResult(bool success, string error) {
            Success = success;
            Error = error ?? string.Empty;
        }

        public bool Success { get; }
        public string Error { get; }

        public static ExplorerActionResult Ok() {
            return new ExplorerActionResult(true, string.Empty);
        }

        public static ExplorerActionResult Fail(string error) {
            return new ExplorerActionResult(false, error);
        }
    }
}
