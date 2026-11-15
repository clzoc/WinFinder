using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinFinder.Backend;
using WinFinder.Utilities;

namespace WinFinder.Controls {
    public class WorkspaceTabHost : UserControl {
        private readonly List<TabEntry> tabs = new List<TabEntry>();
        private readonly ScrollViewer tabScroller;
        private readonly StackPanel tabList;
        private readonly Button addButton;
        private readonly Grid paneHost;
        private readonly Border stripBorder;
        private ThemePalette palette = ThemePalettes.CreateLight();
        private int activeIndex;
        private bool suppressSessionChanged;

        public event EventHandler ActivePaneChanged;
        public event EventHandler HostActivated;
        public event EventHandler SessionChanged;

        public WorkspaceTabHost() {
            Focusable = true;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            stripBorder = new Border {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4, 3, 4, 2)
            };

            var strip = new Grid();
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            tabList = new StackPanel {
                Orientation = Orientation.Horizontal
            };
            tabScroller = new ScrollViewer {
                Content = tabList,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false
            };
            tabScroller.PreviewMouseWheel += TabScroller_PreviewMouseWheel;
            tabScroller.MouseLeftButtonDown += TabStrip_MouseLeftButtonDown;
            Grid.SetColumn(tabScroller, 0);
            strip.Children.Add(tabScroller);

            addButton = new Button {
                Content = "+",
                Width = 24,
                Height = 22,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                Cursor = Cursors.Hand,
                Focusable = false,
                ToolTip = LocalizationStrings.MenuNewTab
            };
            addButton.Click += (s, e) => NewTab();
            Grid.SetColumn(addButton, 1);
            strip.Children.Add(addButton);

            stripBorder.Child = strip;
            Grid.SetRow(stripBorder, 0);
            root.Children.Add(stripBorder);

            paneHost = new Grid();
            Grid.SetRow(paneHost, 1);
            root.Children.Add(paneHost);

            Content = root;
            ApplyTheme(palette);
            NewTab(SessionStore.GetDefaultStartPath(), "list");
        }

        public WorkspacePane ActivePane {
            get {
                if (activeIndex < 0 || activeIndex >= tabs.Count) {
                    return null;
                }
                return tabs[activeIndex].Pane;
            }
        }

        public int TabCount => tabs.Count;

        public bool Contains(WorkspacePane pane) {
            return IndexOf(pane) >= 0;
        }

        public void ApplyTheme(ThemePalette themePalette) {
            if (themePalette == null) {
                return;
            }

            palette = themePalette;
            Background = palette.PaneBackground;
            paneHost.Background = palette.PaneBackground;
            stripBorder.Background = palette.TopBarBackground;
            stripBorder.BorderBrush = palette.DividerBrush;
            addButton.Background = palette.ControlBackground;
            addButton.Foreground = palette.PrimaryText;
            foreach (TabEntry tab in tabs) {
                tab.Pane.ApplyTheme(palette);
            }
            RefreshTabChrome();
        }

        public void NewTab() {
            WorkspacePane current = ActivePane;
            string path = current != null && !string.IsNullOrWhiteSpace(current.CurrentPath)
                ? current.CurrentPath
                : SessionStore.GetDefaultStartPath();
            string view = current != null && current.IsGridView ? "grid" : "list";
            NewTab(path, view);
        }

        public void NewTab(string path, string view) {
            AddTab(path, string.Equals(view, "grid", StringComparison.OrdinalIgnoreCase), true);
        }

        public void DuplicateActiveTab() {
            WorkspacePane current = ActivePane;
            if (current == null) {
                NewTab();
                return;
            }

            NewTab(current.CurrentPath, current.IsGridView ? "grid" : "list");
        }

        public void CloseActiveTab() {
            CloseTabAt(activeIndex);
        }

        public void CloseOtherTabs() {
            if (tabs.Count <= 1) {
                return;
            }

            suppressSessionChanged = true;
            try {
                int keep = activeIndex;
                for (int i = tabs.Count - 1; i >= 0; i--) {
                    if (i != keep) {
                        RemoveTabAt(i);
                        if (i < keep) {
                            keep--;
                        }
                    }
                }
                activeIndex = 0;
                ShowActivePane();
                RefreshTabChrome();
            } finally {
                suppressSessionChanged = false;
            }

            RaiseSessionChanged();
        }

        public void ActivateNext() {
            if (tabs.Count == 0) {
                return;
            }
            ActivateAt((activeIndex + 1) % tabs.Count);
        }

        public void ActivatePrevious() {
            if (tabs.Count == 0) {
                return;
            }
            ActivateAt((activeIndex - 1 + tabs.Count) % tabs.Count);
        }

        public PaneSessionState CaptureState() {
            var state = new PaneSessionState {
                ActiveTabIndex = activeIndex,
                Tabs = new List<WorkspaceTabState>()
            };
            foreach (TabEntry tab in tabs) {
                state.Tabs.Add(new WorkspaceTabState {
                    Path = tab.Pane.CurrentPath,
                    View = tab.Pane.IsGridView ? "grid" : "list"
                });
            }
            return SessionStore.NormalizePane(state, false, SessionStore.GetDefaultStartPath());
        }

        public void RestoreState(PaneSessionState state) {
            PaneSessionState normalized = SessionStore.NormalizePane(state, true, SessionStore.GetDefaultStartPath());
            suppressSessionChanged = true;
            try {
                for (int i = tabs.Count - 1; i >= 0; i--) {
                    RemoveTabAt(i);
                }

                foreach (WorkspaceTabState tab in normalized.Tabs) {
                    AddTab(tab.Path, string.Equals(tab.View, "grid", StringComparison.OrdinalIgnoreCase), false);
                }

                ActivateAt(normalized.ActiveTabIndex);
            } finally {
                suppressSessionChanged = false;
            }
        }

        private void AddTab(string path, bool gridView, bool activate) {
            var pane = new WorkspacePane();
            pane.ApplyTheme(palette);
            if (!string.IsNullOrWhiteSpace(path)) {
                pane.ChangeDirectory(path);
            }
            if (gridView) {
                pane.ShowGridView();
            } else {
                pane.ShowListView();
            }

            pane.PaneActivated += Pane_PaneActivated;
            pane.PropertyChanged += Pane_PropertyChanged;

            var title = new TextBlock {
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 140,
                Margin = new Thickness(0, 0, 6, 0)
            };
            var close = new Button {
                Content = "×",
                Width = 16,
                Height = 16,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                Cursor = Cursors.Hand,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = LocalizationStrings.MenuCloseTab
            };
            var header = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(title);
            header.Children.Add(close);

            var chrome = new Border {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 6, 2),
                Margin = new Thickness(1, 0, 1, 0),
                Cursor = Cursors.Hand,
                Child = header,
                MinWidth = 72
            };

            var entry = new TabEntry {
                Pane = pane,
                Chrome = chrome,
                Title = title,
                Close = close
            };

            chrome.MouseLeftButtonDown += (s, e) => {
                ActivatePane(pane);
                if (e.ClickCount == 1) {
                    e.Handled = true;
                }
            };
            chrome.MouseDown += (s, e) => {
                if (e.ChangedButton == MouseButton.Middle) {
                    CloseTabEntry(entry);
                    e.Handled = true;
                }
            };
            chrome.ContextMenu = CreateTabContextMenu(entry);
            close.Click += (s, e) => {
                CloseTabEntry(entry);
                e.Handled = true;
            };

            pane.Visibility = Visibility.Collapsed;
            paneHost.Children.Add(pane);
            tabList.Children.Add(chrome);
            tabs.Add(entry);
            UpdateTabTitle(entry);

            if (activate || tabs.Count == 1) {
                ActivateAt(tabs.Count - 1);
            } else {
                RefreshTabChrome();
            }

            RaiseSessionChanged();
        }

        private ContextMenu CreateTabContextMenu(TabEntry entry) {
            var menu = new ContextMenu();
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuNewTab, (s, e) => NewTab()));
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuDuplicateTab, (s, e) => {
                ActivatePane(entry.Pane);
                DuplicateActiveTab();
            }));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuCloseTab, (s, e) => CloseTabEntry(entry)));
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuCloseOtherTabs, (s, e) => {
                ActivatePane(entry.Pane);
                CloseOtherTabs();
            }));
            return menu;
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler) {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            return item;
        }

        private void CloseTabEntry(TabEntry entry) {
            int index = tabs.IndexOf(entry);
            if (index >= 0) {
                CloseTabAt(index);
            }
        }

        private void CloseTabAt(int index) {
            if (index < 0 || index >= tabs.Count) {
                return;
            }

            if (tabs.Count == 1) {
                tabs[0].Pane.ShowListView();
                tabs[0].Pane.ChangeDirectory(SessionStore.GetDefaultStartPath());
                UpdateTabTitle(tabs[0]);
                RaiseSessionChanged();
                return;
            }

            int next = index == activeIndex
                ? (index == tabs.Count - 1 ? index - 1 : index)
                : (index < activeIndex ? activeIndex - 1 : activeIndex);
            RemoveTabAt(index);
            ActivateAt(next);
            RaiseSessionChanged();
        }

        private void RemoveTabAt(int index) {
            TabEntry entry = tabs[index];
            entry.Pane.PaneActivated -= Pane_PaneActivated;
            entry.Pane.PropertyChanged -= Pane_PropertyChanged;
            paneHost.Children.Remove(entry.Pane);
            tabList.Children.Remove(entry.Chrome);
            tabs.RemoveAt(index);
        }

        private void ActivatePane(WorkspacePane pane) {
            int index = IndexOf(pane);
            if (index >= 0) {
                ActivateAt(index);
            }
        }

        private void ActivateAt(int index) {
            if (tabs.Count == 0) {
                return;
            }

            if (index < 0) {
                index = 0;
            }
            if (index >= tabs.Count) {
                index = tabs.Count - 1;
            }

            bool changed = index != activeIndex;
            activeIndex = index;
            ShowActivePane();
            RefreshTabChrome();
            FrameworkElement chrome = tabList.Children[activeIndex] as FrameworkElement;
            if (chrome != null) {
                chrome.BringIntoView();
            }

            if (changed && ActivePaneChanged != null) {
                ActivePaneChanged(this, EventArgs.Empty);
            }

            if (HostActivated != null) {
                HostActivated(this, EventArgs.Empty);
            }
        }

        private void ShowActivePane() {
            for (int i = 0; i < tabs.Count; i++) {
                tabs[i].Pane.Visibility = i == activeIndex ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RefreshTabChrome() {
            for (int i = 0; i < tabs.Count; i++) {
                TabEntry tab = tabs[i];
                bool active = i == activeIndex;
                tab.Chrome.Background = active ? palette.ListSelectedBackground : Brushes.Transparent;
                tab.Title.Foreground = active ? palette.ListSelectedText : palette.PrimaryText;
                tab.Close.Foreground = tab.Title.Foreground;
                tab.Close.Background = Brushes.Transparent;
                tab.Close.Visibility = active || tabs.Count == 1 ? Visibility.Visible : Visibility.Visible;
            }
        }

        private void UpdateTabTitle(TabEntry entry) {
            string path = entry.Pane != null ? entry.Pane.CurrentPath : string.Empty;
            string title = SessionStore.FormatTabTitle(path);
            entry.Title.Text = title;
            entry.Chrome.ToolTip = string.IsNullOrWhiteSpace(path) ? title : path;
        }

        private void Pane_PaneActivated(object sender, EventArgs e) {
            WorkspacePane pane = sender as WorkspacePane;
            if (pane != null) {
                ActivatePane(pane);
            }
        }

        private void Pane_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(WorkspacePane.CurrentFolder) && e.PropertyName != nameof(WorkspacePane.CurrentPath)) {
                return;
            }

            WorkspacePane pane = sender as WorkspacePane;
            int index = IndexOf(pane);
            if (index >= 0) {
                UpdateTabTitle(tabs[index]);
                RaiseSessionChanged();
            }
        }

        private void TabScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            tabScroller.ScrollToHorizontalOffset(tabScroller.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void TabStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount >= 2 && e.OriginalSource == tabScroller) {
                NewTab();
                e.Handled = true;
            }
        }

        private int IndexOf(WorkspacePane pane) {
            if (pane == null) {
                return -1;
            }

            for (int i = 0; i < tabs.Count; i++) {
                if (ReferenceEquals(tabs[i].Pane, pane)) {
                    return i;
                }
            }

            return -1;
        }

        private void RaiseSessionChanged() {
            if (!suppressSessionChanged && SessionChanged != null) {
                SessionChanged(this, EventArgs.Empty);
            }
        }

        private sealed class TabEntry {
            public WorkspacePane Pane { get; set; }
            public Border Chrome { get; set; }
            public TextBlock Title { get; set; }
            public Button Close { get; set; }
        }
    }
}
