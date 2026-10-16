using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WinFinder;
using WinFinder.Utilities;

namespace WinFinder.Backend {
    public interface IExplorerProvider {
        bool CanHandle(ExplorerQuery query);
        IReadOnlyList<ExplorerItem> Query(ExplorerQuery query);
    }

    public sealed class FileSystemExplorerProvider : IExplorerProvider {
        public bool CanHandle(ExplorerQuery query) {
            return query != null && query.Kind == ExplorerQueryKind.Browse;
        }

        public IReadOnlyList<ExplorerItem> Query(ExplorerQuery query) {
            if (query == null || query.Kind != ExplorerQueryKind.Browse) {
                return Array.Empty<ExplorerItem>();
            }

            string path = query.Path;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) {
                return Array.Empty<ExplorerItem>();
            }

            try {
                var entries = Directory.EnumerateFileSystemEntries(path)
                    .Select(fullPath => CreateExplorerItem(fullPath))
                    .Where(item => item != null && item.IsVisible)
                    .OrderBy(item => item.IsDirectory ? 0 : 1)
                    .ThenBy(item => item.Name, new NaturalStringComparer())
                    .Skip(Math.Max(0, query.Offset))
                    .Take(Math.Max(0, query.MaxResults))
                    .ToList();

                return entries;
            } catch (Exception ex) {
                Trace.WriteLine($"FileSystemExplorerProvider.Query failed: {ex.Message}");
                return Array.Empty<ExplorerItem>();
            }
        }

        private static ExplorerItem CreateExplorerItem(string fullPath) {
            try {
                FileAttributes attributes = File.GetAttributes(fullPath);
                bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
                long? size = null;
                DateTime? modified = null;

                if (!isDirectory) {
                    var info = new FileInfo(fullPath);
                    size = info.Exists ? (long?)info.Length : null;
                    if (info.Exists) {
                        modified = info.LastWriteTime;
                    }
                } else {
                    var info = new DirectoryInfo(fullPath);
                    if (info.Exists) {
                        modified = info.LastWriteTime;
                    }
                }

                return new ExplorerItem(fullPath, isDirectory, size, modified, attributes);
            } catch {
                return null;
            }
        }
    }

    public sealed class EverythingExplorerProvider : IExplorerProvider {
        public bool CanHandle(ExplorerQuery query) {
            return query != null && query.Kind == ExplorerQueryKind.Search && EverythingApi.IsAvailable;
        }

        public IReadOnlyList<ExplorerItem> Query(ExplorerQuery query) {
            if (query == null || query.Kind != ExplorerQueryKind.Search) {
                return Array.Empty<ExplorerItem>();
            }

            if (!EverythingApi.IsAvailable) {
                return Array.Empty<ExplorerItem>();
            }

            var rawResults = EverythingApi.Query(query.Text, query.Offset, query.MaxResults);

            var results = rawResults
                .Select(result => new ExplorerItem(
                    result.FullPath,
                    result.IsDirectory,
                    result.Size,
                    result.DateModified,
                    result.Attributes))
                .Where(item => item.IsVisible)
                .ToList();

            results.Sort((left, right) => {
                if (left.IsDirectory != right.IsDirectory) {
                    return left.IsDirectory ? -1 : 1;
                }

                var comparer = new NaturalStringComparer();
                return comparer.Compare(left.Name, right.Name);
            });
            return results;
        }
    }
}
