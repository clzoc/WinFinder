using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SharpVectors.Converters;
using WinFinder.Controls;

namespace WinFinder {
    public partial class MainWindow : Window, INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool paneStacked = false;
        private WorkspacePane activePane;
        private int isZoom = 0;
        private readonly string[] zoomIcons = { "/icon/Maximize_Button_Hover_M.svg", "/icon/Maximize_Button_Hover_Zoom_M.svg" };
        private const double SidebarItemHeight = 35;
        private bool useSystemTheme = true;
        private ThemeMode currentTheme = ThemeMode.Light;
        private ThemePalette palette = ThemePalettes.CreateLight();
        private readonly List<SidebarVisual> sidebarVisuals = new List<SidebarVisual>();
        private string selectedSidebarPath = string.Empty;
        private const string PersonalizeRegistryPath = @"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        private const string AppsUseLightThemeValue = "AppsUseLightTheme";

        private class SidebarEntry {
            public string Path { get; set; }
            public string Label { get; set; }
            public string Icon { get; set; }
        }

        private class SidebarVisual {
            public string Path { get; set; }
            public Border Container { get; set; }
            public SvgViewbox Icon { get; set; }
            public TextBlock Label { get; set; }
        }

        private string info = "";
        public string Info {
            get => info;
            set {
                info = value;
                OnPropertyChanged(nameof(Info));
            }
        }

        private string sideClipInfo = "";
        public string SideClipInfo {
            get => sideClipInfo;
            set {
                sideClipInfo = value;
                OnPropertyChanged(nameof(SideClipInfo));
            }
        }

        private string currentFolder = "";
        public string CurrentFolder {
            get => currentFolder;
            set {
                currentFolder = value;
                OnPropertyChanged(nameof(CurrentFolder));
            }
        }

        private string zoomButton = "/icon/Maximize_Button_Hover_M.svg";
        public string ZoomButton {
            get => zoomButton;
            set {
                zoomButton = value;
                OnPropertyChanged(nameof(ZoomButton));
            }
        }

        public MainWindow() {
            InitializeComponent();

            SizeChanged += MainWindow_Resize;
            Closed += MainWindow_Closed;
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            ApplySystemTheme();

            ApplyPaneLayout();
            SetActivePane(Pane1);

            Pane1.PaneActivated += (_, __) => SetActivePane(Pane1);
            Pane2.PaneActivated += (_, __) => SetActivePane(Pane2);
        }

        private void ContentView(object sender, RoutedEventArgs e) {
            UpdateWindowClips();
            BuildSideBar();
        }

        private void SetActivePane(WorkspacePane pane) {
            if (pane == null || pane == activePane) return;

            if (activePane != null) {
                activePane.PropertyChanged -= ActivePane_PropertyChanged;
            }

            activePane = pane;
            activePane.PropertyChanged += ActivePane_PropertyChanged;
            UpdateCurrentFolder();
            UpdateActivePaneVisual();
        }

        private void ActivePane_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(WorkspacePane.CurrentFolder)) {
                UpdateCurrentFolder();
            }
        }

        private void UpdateCurrentFolder() {
            if (activePane != null) {
                var path = activePane.CurrentPath;
                var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(name)) {
                    name = path;
                }
                CurrentFolder = name;
                HighlightSidebarSelection(path);
            }
        }

        private void BuildSideBar() {
            PathBack.Height = 35;
            PathBack.Width = 35;
            PathBack.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            PathBack.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            PathBack.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 0.0));

            PathMove.Height = 35;
            PathMove.Width = 35;
            PathMove.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            PathMove.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            PathMove.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 0.0));

            ViewListView.Height = 35;
            ViewListView.Width = 35;
            ViewListView.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ViewListView.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            ViewListView.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 0.0));

            ViewGridView.Height = 35;
            ViewGridView.Width = 35;
            ViewGridView.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ViewGridView.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            ViewGridView.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 0.0));

            TogglePaneLayout.Height = 35;
            TogglePaneLayout.Width = 35;
            TogglePaneLayout.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            TogglePaneLayout.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            TogglePaneLayout.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 0.0));

            double modeButtonHeight = 30;
            double modeButtonWidth = (SideBar.ActualWidth > 0 ? (SideBar.ActualWidth - 16 - 12) / 3 : 60);
            var modeClip = Geometry.Parse(Window_Corner(modeButtonHeight, modeButtonWidth, modeButtonHeight * 0.40, 0.0));
            ModeAutoButton.Height = modeButtonHeight;
            ModeAutoButton.Width = modeButtonWidth;
            ModeLightButton.Height = modeButtonHeight;
            ModeLightButton.Width = modeButtonWidth;
            ModeDarkButton.Height = modeButtonHeight;
            ModeDarkButton.Width = modeButtonWidth;
            ModeAutoButton.Clip = modeClip;
            ModeLightButton.Clip = modeClip;
            ModeDarkButton.Clip = modeClip;

            SideClipInfo = Window_Corner(SidebarItemHeight, SideBar.ActualWidth > 0 ? SideBar.ActualWidth - 8 : 200, SidebarItemHeight * 0.40, 0.5);

            var entries = BuildSidebarEntries().ToList();
            SideBar.Children.Clear();
            sidebarVisuals.Clear();

            foreach (var entry in entries) {
                var container = new Border {
                    Height = SidebarItemHeight,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(SidebarItemHeight * 0.35),
                    Padding = new Thickness(10, 6, 12, 6),
                    Margin = new Thickness(4, 4, 4, 0),
                    Tag = entry.Path
                };
                BindingOperations.SetBinding(container, ClipProperty, new Binding("Data") { Source = SideClip });

                var row = new Grid { Margin = new Thickness(2, 0, 2, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var icon = new SvgViewbox {
                    Width = 24,
                    Height = 24,
                    Source = new Uri(entry.Icon, UriKind.Relative),
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                };
                Grid.SetColumn(icon, 0);

                var textBlock = new TextBlock {
                    Text = entry.Label,
                    FontSize = 18,
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Foreground = palette.SidebarText,                    
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(5, 0, 0, 0),
                };
                Grid.SetColumn(textBlock, 1);

                row.Children.Add(icon);
                row.Children.Add(textBlock);
                container.Child = row;

                container.MouseEnter += SidebarItem_MouseEnter;
                container.MouseLeave += SidebarItem_MouseLeave;
                container.PreviewMouseLeftButtonDown += SidebarItem_Click;

                SideBar.Children.Add(container);
                sidebarVisuals.Add(new SidebarVisual {
                    Path = entry.Path,
                    Container = container,
                    Icon = icon,
                    Label = textBlock
                });
            }

            HighlightSidebarSelection(activePane?.CurrentPath);
        }

        private IEnumerable<SidebarEntry> BuildSidebarEntries() {
            var items = new List<SidebarEntry>();
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (Directory.Exists(userProfile)) {
                items.Add(new SidebarEntry { Path = userProfile, Label = Environment.UserName, Icon = "/icon/house.svg" });
            }

            AddIfExists(items, Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "桌面", "/icon/laptopcomputer.svg");
            AddIfExists(items, Path.Combine(userProfile, "Downloads"), "下载", "/icon/icloud.and.arrow.down.svg");
            AddIfExists(items, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "音乐", "/icon/headphones.svg");
            AddIfExists(items, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "图片", "/icon/camera.svg");
            AddIfExists(items, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "视频", "/icon/film.svg");
            AddIfExists(items, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "文稿", "/icon/doc.text.svg");

            var drives = DriveInfo.GetDrives()
                .Where(drive => drive.DriveType == DriveType.Fixed)
                .OrderBy(drive => drive.Name);

            foreach (var drive in drives) {
                if (!drive.IsReady) continue;
                var label = $"盘({drive.Name.TrimEnd(Path.DirectorySeparatorChar)})";
                items.Add(new SidebarEntry {
                    Path = drive.RootDirectory.FullName,
                    Label = label,
                    Icon = "/icon/internaldrive.svg"
                });
            }

            return items;
        }

        private void AddIfExists(ICollection<SidebarEntry> items, string path, string label, string icon) {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) {
                items.Add(new SidebarEntry { Path = path, Label = label, Icon = icon });
            }
        }

        private void SidebarItem_MouseEnter(object sender, MouseEventArgs e) {
            if (sender is Border border) {
                string path = border.Tag as string;
                if (!IsSidebarSelected(path)) {
                    border.Background = palette.SidebarHover;
                    SetSidebarForeground(border, palette.SidebarText);
                }
            }
        }

        private void SidebarItem_MouseLeave(object sender, MouseEventArgs e) {
            if (sender is Border border) {
                string path = border.Tag as string;
                if (!IsSidebarSelected(path)) {
                    border.Background = Brushes.Transparent;
                    SetSidebarForeground(border, palette.SidebarText);
                }
            }
        }

        private void SidebarItem_Click(object sender, MouseButtonEventArgs e) {
            if (sender is FrameworkElement fe && fe.Tag is string path && activePane != null) {
                activePane.ChangeDirectory(path);
                HighlightSidebarSelection(path);
            }
        }

        private void HighlightSidebarSelection(string path) {
            selectedSidebarPath = path ?? string.Empty;
            if (!sidebarVisuals.Any() || palette == null) return;

            foreach (var visual in sidebarVisuals) {
                bool isMatch = IsSidebarSelected(visual.Path);
                visual.Container.Background = isMatch ? palette.SidebarSelected : Brushes.Transparent;
                visual.Label.Foreground = isMatch ? palette.SidebarSelectedText : palette.SidebarText;
            }
        }

        private bool IsSidebarSelected(string path) {
            if (string.IsNullOrWhiteSpace(selectedSidebarPath) || string.IsNullOrWhiteSpace(path)) return false;
            return string.Equals(
                selectedSidebarPath.TrimEnd(Path.DirectorySeparatorChar),
                path.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private void SetSidebarForeground(Border container, Brush brush) {
            var visual = sidebarVisuals.FirstOrDefault(v => v.Container == container);
            if (visual != null) {
                visual.Label.Foreground = brush;
            }
        }

        private void MainWindow_Resize(object sender, EventArgs e) {
            UpdateWindowClips();
        }

        private void UpdateWindowClips() {
            Info = Window_Corner(ActualHeight, ActualWidth, 15, 1);
            SideClipInfo = Window_Corner(SidebarItemHeight, SideBar.ActualWidth > 0 ? SideBar.ActualWidth - 8 : 200, SidebarItemHeight * 0.40, 0.5);
        }

        private void ApplySystemTheme() {
            var mode = DetectSystemTheme();
            ApplyTheme(mode);
        }

        private ThemeMode DetectSystemTheme() {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath)) {
                    var value = key?.GetValue(AppsUseLightThemeValue);
                    if (value is int intValue) {
                        return intValue == 0 ? ThemeMode.Dark : ThemeMode.Light;
                    }
                }
            } catch {
                // fall back to light if detection fails
            }
            return ThemeMode.Light;
        }

        private void ApplyTheme(ThemeMode mode) {
            currentTheme = mode;
            palette = mode == ThemeMode.Dark ? ThemePalettes.CreateDark() : ThemePalettes.CreateLight();
            ApplyPalette();
        }

        private void ApplyPalette() {
            if (palette == null) return;

            Resources["WindowBackgroundBrush"] = palette.WindowBackground;
            Resources["TopBarBackgroundBrush"] = palette.TopBarBackground;
            Resources["DividerBrush"] = palette.DividerBrush;
            Resources["SidebarBackgroundBrush"] = palette.SidebarBackground;
            Resources["ControlBackgroundBrush"] = palette.PaneInactiveBackground;
            Resources["ControlHoverBrush"] = palette.ListHoverBackground;

            LayoutRootParent.Background = palette.WindowBackground;
            LayoutNodeTop.Background = palette.TopBarBackground;
            if (TopDivider != null) {
                TopDivider.Stroke = palette.DividerBrush;
            }
            if (LeftDivider != null) {
                LeftDivider.Stroke = palette.DividerBrush;
            }
            LeftSideOutside.Background = palette.SidebarBackground;

            currentFolderRegion.Foreground = palette.PrimaryText;

            ApplySidebarPalette();
            ApplyPanePalette();
            UpdateModeButtonsAppearance();
        }

        private void ApplySidebarPalette() {
            var sidebarTitle = LeftSide.Children.OfType<TextBlock>().FirstOrDefault();
            if (sidebarTitle != null) {
                sidebarTitle.Foreground = palette.MutedText;
            }
            foreach (var visual in sidebarVisuals) {
                visual.Label.Foreground = IsSidebarSelected(visual.Path) ? palette.SidebarSelectedText : palette.SidebarText;
            }
            HighlightSidebarSelection(selectedSidebarPath);
        }

        private void ApplyPanePalette() {
            if (Pane1 != null) {
                Pane1.ApplyTheme(palette);
            }
            if (Pane2 != null) {
                Pane2.ApplyTheme(palette);
            }
            UpdateActivePaneVisual();
        }

        private void TogglePaneLayout_Click(object sender, RoutedEventArgs e) {
            paneStacked = !paneStacked;
            ApplyPaneLayout();
        }

        private void ApplyPaneLayout() {
            if (PaneContainer == null || Pane1Host == null || Pane2Host == null) return;

            if (paneStacked) {
                PaneContainer.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                PaneContainer.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                PaneContainer.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                PaneContainer.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);

                Grid.SetRow(Pane1Host, 0);
                Grid.SetColumn(Pane1Host, 0);
                Grid.SetColumnSpan(Pane1Host, 2);
                Grid.SetRow(Pane2Host, 1);
                Grid.SetColumn(Pane2Host, 0);
                Grid.SetColumnSpan(Pane2Host, 2);
            } else {
                PaneContainer.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                PaneContainer.RowDefinitions[1].Height = new GridLength(0);
                PaneContainer.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                PaneContainer.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

                Grid.SetRow(Pane1Host, 0);
                Grid.SetColumn(Pane1Host, 0);
                Grid.SetColumnSpan(Pane1Host, 1);
                Grid.SetRow(Pane2Host, 0);
                Grid.SetColumn(Pane2Host, 1);
                Grid.SetColumnSpan(Pane2Host, 1);
            }

            PaneContainer.InvalidateMeasure();
            PaneContainer.UpdateLayout();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            DragMove();
        }

        private void Window_Close(object sender, RoutedEventArgs e) {
            Close();
        }

        private void Window_Minim(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void Window_Zoom(object sender, RoutedEventArgs e) {
            if (isZoom == 0) {
                Height = SystemParameters.WorkArea.Height;
                Width = SystemParameters.WorkArea.Width;
                Top = 0;
                Left = 0;
                isZoom = 1;
            } else {
                double h = Height;
                double w = Width;
                Height = 900 + 2;
                Width = 1200 + 2;
                Top = 0.5 * (h - Height);
                Left = 0.5 * (w - Width);
                isZoom = 0;
            }
            ZoomButton = zoomIcons[isZoom];
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) {
            if (!useSystemTheme) return;
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.VisualStyle) {
                Dispatcher.Invoke(ApplySystemTheme);
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e) {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        }

        private void Window_Back(object sender, RoutedEventArgs e) {
            activePane?.NavigateBack();
        }

        private void Window_Retu(object sender, RoutedEventArgs e) {
            activePane?.NavigateForward();
        }

        private void ViewListView_Click(object sender, RoutedEventArgs e) {
            activePane?.ShowListView();
        }

        private void ViewGridView_Click(object sender, RoutedEventArgs e) {
            activePane?.ShowGridView();
        }

        private void UpdateActivePaneVisual() {
            if (Pane1Host == null || Pane2Host == null || palette == null || activePane == null) return;
            var activeHost = activePane == Pane1 ? Pane1Host : Pane2Host;
            var inactiveHost = activePane == Pane1 ? Pane2Host : Pane1Host;

            activeHost.BorderBrush = palette.PaneActiveBorder;
            activeHost.BorderThickness = new Thickness(1);
            activeHost.Background = palette.PaneActiveBackground;
            activeHost.Effect = null;

            inactiveHost.BorderBrush = palette.PaneBorder;
            inactiveHost.BorderThickness = new Thickness(1);
            inactiveHost.Background = palette.PaneInactiveBackground;
            inactiveHost.Effect = null;
        }

        private void UpdateModeButtonsAppearance() {
            if (ModeAutoButton == null || ModeLightButton == null || ModeDarkButton == null || palette == null) return;

            Brush activeBg = palette.SidebarSelected;
            Brush inactiveBg = palette.SidebarBackground;
            Brush activeFg = palette.SidebarSelectedText;
            Brush inactiveFg = palette.SidebarText;

            bool isLight = currentTheme == ThemeMode.Light;
            bool isDark = currentTheme == ThemeMode.Dark;

            ModeAutoButton.Background = useSystemTheme ? activeBg : inactiveBg;
            ModeLightButton.Background = (!useSystemTheme && isLight) ? activeBg : inactiveBg;
            ModeDarkButton.Background = (!useSystemTheme && isDark) ? activeBg : inactiveBg;

            ModeAutoButton.Foreground = useSystemTheme ? activeFg : inactiveFg;
            ModeLightButton.Foreground = (!useSystemTheme && isLight) ? activeFg : inactiveFg;
            ModeDarkButton.Foreground = (!useSystemTheme && isDark) ? activeFg : inactiveFg;
        }

        private void ModeAuto_Click(object sender, RoutedEventArgs e) {
            useSystemTheme = true;
            ApplySystemTheme();
            UpdateModeButtonsAppearance();
        }

        private void ModeLight_Click(object sender, RoutedEventArgs e) {
            useSystemTheme = false;
            ApplyTheme(ThemeMode.Light);
            UpdateModeButtonsAppearance();
        }

        private void ModeDark_Click(object sender, RoutedEventArgs e) {
            useSystemTheme = false;
            ApplyTheme(ThemeMode.Dark);
            UpdateModeButtonsAppearance();
        }

        public string Window_Corner(double height, double width, double radius, double bias) {
            height -= bias * 2; width -= bias * 2;

            double f5 = 63.0000;
            double f4 = 36.7519;
            double f3 = 23.6278;
            double f2 = 14.4275;
            double f1 = 6.6844;
            double f0 = 0;

            double ratio = radius / 35;

            f0 *= ratio; f1 *= ratio; f2 *= ratio; f3 *= ratio; f4 *= ratio; f5 *= ratio;

            double w0 = width - f0;
            double w1 = width - f1;
            double w2 = width - f2;
            double w3 = width - f3;
            double w4 = width - f4;
            double w5 = width - f5;

            double h0 = height - f0;
            double h1 = height - f1;
            double h2 = height - f2;
            double h3 = height - f3;
            double h4 = height - f4;
            double h5 = height - f5;

            f5 += bias; f4 += bias; f3 += bias; f2 += bias; f1 += bias; f0 += bias;

            w0 += bias; w1 += bias; w2 += bias; w3 += bias; w4 += bias; w5 += bias;
            h0 += bias; h1 += bias; h2 += bias; h3 += bias; h4 += bias; h5 += bias;

            string left_top = $"M{f0},{f5} C{f0},{f4} {f0},{f3} {f1},{f2} A{radius},{radius} 0 0 1 {f2},{f1} C{f3},{f0} {f4},{f0} {f5},{f0}";
            string right_top = $"L{w5},{f0} C{w4},{f0} {w3},{f0} {w2},{f1} A{radius},{radius} 0 0 1 {w1},{f2} C{w0},{f3} {w0},{f4} {w0},{f5}";
            string right_bottom = $"L{w0},{h5} C{w0},{h4} {w0},{h3} {w1},{h2} A{radius},{radius} 0 0 1 {w2},{h1} C{w3},{h0} {w4},{h0} {w5},{h0}";
            string left_bottom = $"L{f5},{h0} C{f4},{h0} {f3},{h0} {f2},{h1} A{radius},{radius} 0 0 1 {f1},{h2} C{f0},{h3} {f0},{h4} {f0},{h5} L{f0},{f5} Z";

            return left_top + right_top + right_bottom + left_bottom;
        }

        private void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
