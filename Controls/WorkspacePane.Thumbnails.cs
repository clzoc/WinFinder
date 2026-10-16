using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using WinFinder.Backend;
using WinFinder.Utilities;

namespace WinFinder.Controls {
    public partial class WorkspacePane {
        private int itemsNum = 0;
        public double offsetScroll = 0;
        private long thumbnailRequestGeneration;
        private long thumbnailRequestId;
        private readonly object thumbnailCancellationSync = new object();
        private readonly SemaphoreSlim thumbnailConcurrency = new SemaphoreSlim(4, 4);
        private CancellationTokenSource thumbnailCancellation = new CancellationTokenSource();

        private void Timer_Tick(object sender, EventArgs e) {
            (sender as DispatcherTimer)?.Stop();

            long generation = Interlocked.Read(ref thumbnailRequestGeneration);
            CancellationToken cancellationToken = GetThumbnailCancellationToken();
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            ItemCollection viewItems;
            int rowIndex = (int)Math.Floor(offsetScroll / 30);
            if (GridViewContainer.IsVisible) {
                int rowIndexGrid = (int)Math.Floor(offsetScroll / gridHeight);
                rowIndex = rowIndexGrid * (isZoom == 0 ? 8 : 17);
                viewItems = GridViewContainer.Items;
            } else {
                viewItems = FILEINFOMATION.Items;
            }

            var workItems = new List<ThumbnailWorkItem>();
            int itemCount = viewItems.Count;
            for (int i = 0; i < 136; i++) {
                int localIndex = i + rowIndex;
                if (localIndex < 0 || localIndex >= itemCount) {
                    continue;
                }

                if (!(viewItems[localIndex] is MyStruct item) || item.ThumbLoaded || item.ThumbLoading) {
                    continue;
                }

                bool isDirectory = string.Equals(item.X2, LocalizationStrings.FolderType, StringComparison.OrdinalIgnoreCase);
                if (item.S0 == null) {
                    item.S0 = PlaceholderIconFactory.GetPlaceholder(item.X2, isDirectory);
                }

                string fullPath = GetItemFullPath(item);
                if (string.IsNullOrWhiteSpace(fullPath)) {
                    item.ThumbLoaded = true;
                    continue;
                }

                long requestId = Interlocked.Increment(ref thumbnailRequestId);
                item.ThumbLoading = true;
                item.ThumbRequestId = requestId;

                workItems.Add(new ThumbnailWorkItem {
                    Item = item,
                    FullPath = fullPath,
                    Name = item.X0 ?? string.Empty,
                    Format = item.X2 ?? string.Empty,
                    IsDirectory = isDirectory,
                    RequestId = requestId,
                    Generation = generation,
                    CancellationToken = cancellationToken
                });
            }

            if (workItems.Count == 0) {
                return;
            }

            foreach (var workItem in workItems) {
                _ = RunThumbnailWorkItemAsync(workItem);
            }
        }

        private sealed class ThumbnailWorkItem {
            public MyStruct Item { get; set; }
            public string FullPath { get; set; }
            public string Name { get; set; }
            public string Format { get; set; }
            public bool IsDirectory { get; set; }
            public long RequestId { get; set; }
            public long Generation { get; set; }
            public CancellationToken CancellationToken { get; set; }
        }

        private async Task RunThumbnailWorkItemAsync(ThumbnailWorkItem workItem) {
            try {
                await thumbnailConcurrency.WaitAsync(workItem.CancellationToken);
                try {
                    LoadThumbnail(workItem);
                } finally {
                    thumbnailConcurrency.Release();
                }
            } catch (OperationCanceledException) {
                SetThumbnail(workItem, ThumbnailResult.Pending(diagnostic: "Thumbnail request was cancelled."));
            } catch (Exception ex) {
                Trace.WriteLine($"{workItem?.FullPath} {ex.Message}");
                SetThumbnail(workItem, ThumbnailResult.Error(diagnostic: ex.Message));
            }
        }

        private void LoadThumbnail(ThumbnailWorkItem workItem) {
            if (workItem == null || workItem.Item == null) {
                return;
            }

            if (workItem.CancellationToken.IsCancellationRequested) {
                SetThumbnail(workItem, ThumbnailResult.Pending(diagnostic: "Thumbnail request was cancelled."));
                return;
            }

            ThumbnailResult result = ExplorerBackend.Default.GetThumbnailResult(new ThumbnailRequest(
                workItem.FullPath,
                workItem.Name,
                workItem.Format,
                workItem.IsDirectory,
                210,
                ThumbnailPriority.Visible,
                4000,
                workItem.CancellationToken));

            SetThumbnail(workItem, result);
        }

        private void SetThumbnail(ThumbnailWorkItem workItem, ThumbnailResult result) {
            if (workItem == null || workItem.Item == null || result == null || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) {
                return;
            }

            Dispatcher.InvokeAsync(new Action(() => {
                MyStruct item = workItem.Item;
                bool staleRequest = workItem.Generation != Interlocked.Read(ref thumbnailRequestGeneration)
                    || workItem.CancellationToken.IsCancellationRequested
                    || item.ThumbRequestId != workItem.RequestId
                    || !string.Equals(GetItemFullPath(item), workItem.FullPath, StringComparison.OrdinalIgnoreCase);

                if (staleRequest) {
                    if (item.ThumbRequestId == workItem.RequestId) {
                        item.ThumbLoading = false;
                        item.ThumbRequestId = 0;
                    }
                    return;
                }

                item.ThumbLoading = false;
                if (result.HasImage) {
                    item.S0 = result.Image;
                }

                if (result.IsTerminal) {
                    item.ThumbLoaded = true;
                }
            }));
        }

        private CancellationToken GetThumbnailCancellationToken() {
            lock (thumbnailCancellationSync) {
                return thumbnailCancellation.Token;
            }
        }

        private void ResetThumbnailScheduling() {
            Interlocked.Increment(ref thumbnailRequestGeneration);
            Interlocked.Exchange(ref thumbnailRequestId, 0);
            lock (thumbnailCancellationSync) {
                thumbnailCancellation.Cancel();
                thumbnailCancellation = new CancellationTokenSource();
            }
            if (timer.IsEnabled) {
                timer.Stop();
            }
        }

        private void ResetThumbnailState(MyStruct item) {
            if (item == null) {
                return;
            }

            item.ThumbLoaded = false;
            item.ThumbLoading = false;
            item.ThumbRequestId = 0;
            item.S0 = null;
        }

        private void ForScrollChanged(object sender, ScrollChangedEventArgs e) {
            offsetScroll = e.VerticalOffset;
            if (timer.IsEnabled) {
                timer.Stop();
            }
            timer.Start();
        }
    }
}
