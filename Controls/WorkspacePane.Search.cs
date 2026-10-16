using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinFinder.Backend;
using WinFinder.Utilities;

namespace WinFinder.Controls {
    public partial class WorkspacePane {
        public void Search(string query) {
            _ = SearchAsync(query);
        }

        private async Task SearchAsync(string query) {
            query = query ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query)) {
                ExitSearch();
                return;
            }

            long generation;
            CancellationToken cancellationToken = BeginContentLoad(out generation);
            ResetThumbnailScheduling();

            IsSearchMode = true;
            SearchQuery = query;
            StopDirectoryWatcher();

            CloseBreadcrumbPopup();
            CloseInlineBreadcrumbPopup();
            pwdInfo.Children.Clear();

            CurrentFolder = LocalizationStrings.SearchResults(query);

            try {
                var entries = await ExplorerBackend.Default.SearchAsync(query, cancellationToken: cancellationToken);
                if (!IsCurrentContentLoad(generation, cancellationToken)
                    || !IsSearchMode
                    || !string.Equals(SearchQuery, query, StringComparison.Ordinal)) {
                    return;
                }

                ResetThumbnailScheduling();
                ClearListInfoItems();
                itemsNum = 0;

                var visibleEntries = entries.Where(t => t.IsVisible).ToList();
                foreach (var entry in visibleEntries) {
                    ListInfo.Add(new MyStruct {
                        ThumbLoaded = false,
                        ThumbLoading = false,
                        FullPath = entry.FullPath,
                        X0 = entry.Name,
                        X1 = entry.Modified?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                        X2 = entry.IsDirectory ? LocalizationStrings.FolderType : entry.Extension,
                        X3 = entry.IsDirectory ? new string[2] { string.Empty, string.Empty } : ByteToValue(entry.SizeBytes ?? 0),
                        X4 = entry.IsDirectory ? -1 : (entry.SizeBytes ?? 0)
                    });
                }

                itemsNum = visibleEntries.Count;
                SelectedCount = 0;
                ExtraInfo.Text = LocalizationStrings.ItemsSummary(visibleEntries.Count, 0);

                ScheduleVisibleThumbnailLoad(visibleEntries.Count);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Debug.WriteLine($"SearchAsync failed: {ex.Message}");
                if (IsCurrentContentLoad(generation, cancellationToken) && IsSearchMode) {
                    ClearListInfoItems();
                    itemsNum = 0;
                    SelectedCount = 0;
                    ExtraInfo.Text = LocalizationStrings.ItemsSummary(0, 0);
                }
            }
        }

        public void ExitSearch() {
            if (!IsSearchMode && string.IsNullOrWhiteSpace(SearchQuery)) {
                return;
            }

            IsSearchMode = false;
            SearchQuery = string.Empty;
            ChangeDirectory(string.IsNullOrWhiteSpace(pwd) ? GetDefaultStartPath() : pwd);
        }

        private void ActivateItem(MyStruct item) {
            if (item == null) {
                return;
            }

            string fullPath = GetItemFullPath(item);
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return;
            }

            if (string.Equals(item.X2, LocalizationStrings.FolderType, StringComparison.OrdinalIgnoreCase)) {
                ChangeDirectory(fullPath);
                return;
            }

            ExplorerBackend.Default.ExecuteAction(new ExplorerActionRequest(ExplorerActionKind.Open, fullPath));
        }
    }
}
