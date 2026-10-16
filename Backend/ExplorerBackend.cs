using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WinFinder.Utilities;

namespace WinFinder.Backend {
    public sealed class ExplorerBackend {
        private readonly IExplorerProvider browseProvider = new FileSystemExplorerProvider();
        private readonly IExplorerProvider searchProvider = new EverythingExplorerProvider();
        private readonly ThumbnailClientService thumbnailService = new ThumbnailClientService();

        public static ExplorerBackend Default { get; } = new ExplorerBackend();

        private ExplorerBackend() {
        }

        public bool IsEverythingAvailable => EverythingApi.IsSdkAvailable;
        public ThumbnailClientHealth ThumbnailServiceHealth => thumbnailService.GetHealth();

        public BitmapSource GetThumbnail(ThumbnailRequest request) {
            try {
                return thumbnailService.GetThumbnail(request);
            } catch (Exception ex) {
                Trace.WriteLine($"ExplorerBackend.GetThumbnail failed: {ex.Message}");
                return null;
            }
        }

        public ThumbnailResult GetThumbnailResult(ThumbnailRequest request) {
            try {
                return thumbnailService.GetThumbnailResult(request);
            } catch (Exception ex) {
                Trace.WriteLine($"ExplorerBackend.GetThumbnailResult failed: {ex.Message}");
                return ThumbnailResult.Error(diagnostic: ex.Message);
            }
        }

        public IReadOnlyList<ExplorerItem> Browse(string path, int offset = 0, int maxResults = 10000) {
            try {
                return browseProvider.Query(ExplorerQuery.Browse(path, offset, maxResults));
            } catch (Exception ex) {
                Trace.WriteLine($"ExplorerBackend.Browse failed: {ex.Message}");
                return Array.Empty<ExplorerItem>();
            }
        }

        public Task<IReadOnlyList<ExplorerItem>> BrowseAsync(string path, int offset = 0, int maxResults = 10000, CancellationToken cancellationToken = default(CancellationToken)) {
            return Task.Run(() => {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<ExplorerItem> items = Browse(path, offset, maxResults);
                cancellationToken.ThrowIfCancellationRequested();
                return items;
            }, cancellationToken);
        }

        public IReadOnlyList<ExplorerItem> Search(string text, int offset = 0, int maxResults = 200) {
            try {
                return searchProvider.Query(ExplorerQuery.Search(text, offset, maxResults));
            } catch (Exception ex) {
                Trace.WriteLine($"ExplorerBackend.Search failed: {ex.Message}");
                return Array.Empty<ExplorerItem>();
            }
        }

        public Task<IReadOnlyList<ExplorerItem>> SearchAsync(string text, int offset = 0, int maxResults = 200, CancellationToken cancellationToken = default(CancellationToken)) {
            return Task.Run(() => {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<ExplorerItem> items = Search(text, offset, maxResults);
                cancellationToken.ThrowIfCancellationRequested();
                return items;
            }, cancellationToken);
        }

        public ExplorerActionResult ExecuteAction(ExplorerActionRequest request) {
            if (request == null || string.IsNullOrWhiteSpace(request.Path)) {
                return ExplorerActionResult.Fail("Empty path.");
            }

            try {
                switch (request.Kind) {
                    case ExplorerActionKind.Open:
                        System.Diagnostics.Process.Start(new ProcessStartInfo {
                            FileName = request.Path,
                            UseShellExecute = true
                        });
                        return ExplorerActionResult.Ok();
                    case ExplorerActionKind.OpenContainingFolder:
                        System.Diagnostics.Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{request.Path}\"") {
                            UseShellExecute = true
                        });
                        return ExplorerActionResult.Ok();
                    case ExplorerActionKind.CopyPath:
                        Clipboard.SetText(request.Path);
                        return ExplorerActionResult.Ok();
                    default:
                        return ExplorerActionResult.Fail("Unsupported action.");
                }
            } catch (Exception ex) {
                Trace.WriteLine($"ExplorerBackend.ExecuteAction failed: {ex.Message}");
                return ExplorerActionResult.Fail(ex.Message);
            }
        }
    }
}
