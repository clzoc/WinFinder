using SharpVectors.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinFinder;

namespace WinFinder.Controls {
    public partial class WorkspacePane : UserControl, INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler PaneActivated;

        public ObservableCollection<MyStruct> ListInfo = new ObservableCollection<MyStruct>();

        private ThemePalette palette = ThemePalettes.CreateLight();

        private readonly DispatcherTimer timer = new DispatcherTimer {
            Interval = TimeSpan.FromMilliseconds(50)
        };

        private const double squircle_radius = 15;
        private double fileHeight = 30;
        private double gridHeight = 150;
        private const double BreadcrumbPopupWidth = 200;
        private const double BreadcrumbItemHeight = 33;
        private FrameworkElement breadcrumbAnchor;
        private Grid isSublistExist = null;

        private string clipInfo = "";
        public string ClipInfo {
            get => clipInfo;
            set {
                clipInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClipInfo)));
            }
        }

        private string gridClipInfo = "";
        public string GridClipInfo {
            get => gridClipInfo;
            set {
                gridClipInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GridClipInfo)));
            }
        }

        private string gridInnerClipInfo = "";
        public string GridInnerClipInfo {
            get => gridInnerClipInfo;
            set {
                gridInnerClipInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GridInnerClipInfo)));
            }
        }

        private string currentFolder = "";
        public string CurrentFolder {
            get => currentFolder;
            set {
                currentFolder = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentFolder)));
            }
        }

        private int isZoom = 0;
        private string pwd = @"C:\Users\tsunami";
        private string once = @"";

        public string CurrentPath => pwd;

        // selection tracking
        private int _selectedCount = 0;
        public int SelectedCount {
            get => _selectedCount;
            set {
                _selectedCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCount)));
                UpdateExtraInfo();
            }
        }

        public WorkspacePane() {
            InitializeComponent();

            timer.Tick += Timer_Tick;

            ListInfo.CollectionChanged += ListInfo_CollectionChanged;
            foreach (var item in ListInfo) {
                item.PropertyChanged += Item_PropertyChanged;
            }
            FILEINFOMATION.ItemsSource = ListInfo;
            GridViewContainer.ItemsSource = ListInfo;

            ApplyNaturalSort("X0", ListSortDirection.Ascending);

            _autoScrollTimer = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;

            Loaded += WorkspacePane_Loaded;
            SizeChanged += WorkspacePane_SizeChanged;
            RefGrid.SizeChanged += RefGrid_SizeChanged;
            AddHandler(MouseDownEvent, new MouseButtonEventHandler(OnPaneMouseDown), true);
            AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnGlobalPreviewMouseLeftButtonDown), true);
            if (BreadcrumbPopup != null) {
                BreadcrumbPopup.Closed += BreadcrumbPopup_Closed;
            }
            if (BreadcrumbPopupBorder != null) {
                BreadcrumbPopupBorder.SizeChanged += (_, __) => {
                    UpdateBreadcrumbPopupOffset();
                    RefreshBreadcrumbPopupChrome();
                };
            }
        }

        private void OnPaneMouseDown(object sender, MouseButtonEventArgs e) {
            PaneActivated?.Invoke(this, EventArgs.Empty);
        }

        private void BreadcrumbPopup_Closed(object sender, EventArgs e) {
            BreadcrumbPopupPanel?.Children.Clear();
            breadcrumbAnchor = null;
        }

        private void CloseBreadcrumbPopup() {
            if (BreadcrumbPopup != null) {
                BreadcrumbPopup.IsOpen = false;
            }
            BreadcrumbPopupPanel?.Children.Clear();
            breadcrumbAnchor = null;
        }

        private void CloseInlineBreadcrumbPopup() {
            if (isSublistExist != null && WorkSpace.Children.Contains(isSublistExist)) {
                WorkSpace.Children.Remove(isSublistExist);
            }
            isSublistExist = null;
        }

        private void OnGlobalPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (isSublistExist != null && !isSublistExist.IsMouseOver) {
                CloseInlineBreadcrumbPopup();
            }

            if (BreadcrumbPopup != null && BreadcrumbPopup.IsOpen) {
                if (BreadcrumbPopup.Child is FrameworkElement popupRoot) {
                    if (!popupRoot.IsMouseOver) {
                        CloseBreadcrumbPopup();
                    }
                } else {
                    CloseBreadcrumbPopup();
                }
            }
        }

        public void ShowListView() {
            ViewListView_Click(this, new RoutedEventArgs());
        }

        public void ShowGridView() {
            ViewGridView_Click(this, new RoutedEventArgs());
        }

        private void WorkspacePane_Loaded(object sender, RoutedEventArgs e) {
            GridClipInfo = Window_Corner(gridHeight, 115, squircle_radius, 0.5);
            GridInnerClipInfo = Window_Corner(gridHeight, 115, squircle_radius, 0.5);
            UpdateClips();
            ChangeDirectory(pwd);
        }

        private void WorkspacePane_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateClips();
        }

        private void RefGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateClips();
        }

        private void UpdateClips() {
            double listWidth = RefGrid?.ActualWidth > 0 ? RefGrid.ActualWidth - 12 : 0;
            ClipInfo = Window_Corner(fileHeight, listWidth, squircle_radius - 3, 0.25);
        }

        public void ApplyTheme(ThemePalette themePalette) {
            if (themePalette == null) return;
            palette = themePalette;
            ApplyThemeToView();
        }

        private void ApplyThemeToView() {
            WorkSpace.Background = palette.PaneBackground;
            ContentArea.Background = palette.PaneBackground;
            GridViewContainer.Background = palette.PaneBackground;
            FILEINFOMATION.Background = palette.PaneBackground;
            FILEINFOMATION.Foreground = palette.PrimaryText;
            GridViewContainer.Foreground = palette.PrimaryText;

            headerline.Stroke = palette.DividerBrush;
            InfoDivider.Stroke = palette.DividerBrush;
            PathDivider.Stroke = palette.DividerBrush;
            ExtraInfo.Foreground = palette.SecondaryText;
            if (BreadcrumbPopupBorder != null) {
                BreadcrumbPopupBorder.Background = palette.PaneBackground;
                BreadcrumbPopupBorder.BorderBrush = palette.DividerBrush;
            }

            BuildListStyles();
            UpdateBreadcrumbColors();
        }

        private void BuildListStyles() {
            FILEINFOMATION.ItemContainerStyle = BuildListItemStyle();
            GridViewContainer.ItemContainerStyle = BuildGridItemStyle();
        }

        private Style BuildListItemStyle() {
            var style = new Style(typeof(ListViewItem));
            var template = new ControlTemplate(typeof(ListViewItem));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            var templatedParent = new RelativeSource(RelativeSourceMode.TemplatedParent);
            borderFactory.SetBinding(Border.PaddingProperty, new Binding { RelativeSource = templatedParent, Path = new PropertyPath("Padding") });
            borderFactory.SetBinding(Border.BackgroundProperty, new Binding { RelativeSource = templatedParent, Path = new PropertyPath("Background") });
            borderFactory.SetBinding(Border.BorderBrushProperty, new Binding { RelativeSource = templatedParent, Path = new PropertyPath("BorderBrush") });
            borderFactory.SetBinding(Border.BorderThicknessProperty, new Binding { RelativeSource = templatedParent, Path = new PropertyPath("BorderThickness") });
            borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var presenter = new FrameworkElementFactory(typeof(GridViewRowPresenter));
            presenter.SetBinding(FrameworkElement.HorizontalAlignmentProperty, new Binding { RelativeSource = templatedParent, Path = new PropertyPath("HorizontalContentAlignment") });
            presenter.SetBinding(FrameworkElement.VerticalAlignmentProperty, new Binding { RelativeSource = templatedParent, Path = new PropertyPath("VerticalContentAlignment") });
            presenter.SetBinding(UIElement.SnapsToDevicePixelsProperty, new Binding { RelativeSource = templatedParent, Path = new PropertyPath("SnapsToDevicePixels") });

            borderFactory.AppendChild(presenter);
            template.VisualTree = borderFactory;

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(Control.HeightProperty, fileHeight));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.PrimaryText));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.PaneInactiveBackground));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.ClipProperty, new Binding("Data") { Source = FileClip }));
            style.Setters.Add(new Setter(ListViewItem.IsSelectedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay }));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));

            var alternationTrigger = new Trigger { Property = ItemsControl.AlternationIndexProperty, Value = 1 };
            alternationTrigger.Setters.Add(new Setter(Control.BackgroundProperty, palette.ListAlternateBackground));
            style.Triggers.Add(alternationTrigger);

            var hoverTrigger = new Trigger { Property = Control.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, palette.ListHoverBackground));
            style.Triggers.Add(hoverTrigger);

            var selectedTrigger = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, palette.ListSelectedBackground));
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, palette.SidebarSelectedText));
            style.Triggers.Add(selectedTrigger);

            style.Setters.Add(new EventSetter(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Item_MouseDoubleClickForListView)));

            return style;
        }

        private Style BuildGridItemStyle() {
            var style = new Style(typeof(ListViewItem));
            style.Setters.Add(new Setter(Control.WidthProperty, 115.0));
            style.Setters.Add(new Setter(Control.HeightProperty, gridHeight));
            style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 0, 6)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.ClipProperty, new Binding("Data") { Source = GridInnerViewClip }));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.PaneInactiveBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.PrimaryText));
            style.Setters.Add(new Setter(ListViewItem.IsSelectedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay }));
            style.Setters.Add(new EventSetter(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Item_MouseDoubleClickForListView)));

            var hoverTrigger = new Trigger { Property = Control.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, palette.ListHoverBackground));
            style.Triggers.Add(hoverTrigger);

            var selectedTrigger = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, palette.ListSelectedBackground));
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, palette.SidebarSelectedText));
            style.Triggers.Add(selectedTrigger);

            return style;
        }

        private void UpdateBreadcrumbColors() {
            foreach (var element in pwdInfo.Children.OfType<FrameworkElement>()) {
                ApplyBreadcrumbHoverStyle(element);
                if (element is Panel panel) {
                    foreach (var text in panel.Children.OfType<TextBlock>()) {
                        text.Foreground = palette.PrimaryText;
                    }
                }
            }
            ExtraInfo.Foreground = palette.SecondaryText;
        }

        private void ApplyBreadcrumbHoverStyle(FrameworkElement element) {
            var style = new Style(element.GetType());
            var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(BackgroundProperty, palette.BreadcrumbHoverBackground));
            style.Triggers.Add(trigger);
            element.Style = style;
            element.SetCurrentValue(BackgroundProperty, Brushes.Transparent);
            element.Cursor = Cursors.Hand;
        }

        private void PwdInfoScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void ListInfo_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.NewItems != null) {
                foreach (MyStruct item in e.NewItems) {
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            if (e.OldItems != null) {
                foreach (MyStruct item in e.OldItems) {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            UpdateSelectionCount();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(MyStruct.IsSelected)) {
                UpdateSelectionCount();
            }
        }

        private void UpdateSelectionCount() {
            SelectedCount = ListInfo.Count(item => item.IsSelected);
        }

        private void UpdateExtraInfo() {
            try {
                DirectoryInfo di = new DirectoryInfo(@pwd);
                if (DriveInfo.GetDrives().Any(d => d.Name == di.Root.FullName)) {
                    DriveInfo drive = new DriveInfo(di.Root.FullName);
                    string[] p0 = ByteToValue(drive.TotalSize);
                    string[] p1 = ByteToValue(drive.TotalFreeSpace);
                    ExtraInfo.Text = $"共 {p0[0]}{p0[1]} 可用 {p1[0]}{p1[1]} ";
                }
                ExtraInfo.Text += $"共 {ListInfo.Count} 项 已选择 {SelectedCount} 项";
            } catch (Exception ex) {
                Debug.WriteLine($"UpdateExtraInfo failed: {ex.Message}");
                ExtraInfo.Text = "";
            }
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

            string line = left_top + right_top + right_bottom + left_bottom;
            return line;
        }

        public void ChangeDirectory(string path) {
            Change_ItemSource(path);
        }

        public bool NavigateBack() {
            DirectoryInfo di = new DirectoryInfo(@pwd);
            if (di.Parent == null) return false;

            once = pwd;
            Change_ItemSource(di.Parent.FullName);
            return true;
        }

        public bool NavigateForward() {
            if (string.IsNullOrEmpty(once)) return false;
            Change_ItemSource(once);
            return true;
        }

        private void Change_ItemSource(string str) {
            CloseBreadcrumbPopup();
            CloseInlineBreadcrumbPopup();
            FileStream fs = new FileStream(@"E:\Repo\wpf\WpfApp2\ico\dif.png", FileMode.Open);
            BitmapImage bm = new BitmapImage();
            bm.BeginInit();
            bm.DecodePixelWidth = 210;
            bm.StreamSource = fs;
            bm.CacheOption = BitmapCacheOption.OnLoad;
            bm.EndInit();
            fs.Dispose();
            bm.Freeze();

            FileStream ks = new FileStream(@"E:\Repo\wpf\WpfApp2\ico\Macintosh_HD.png", FileMode.Open);
            BitmapImage km = new BitmapImage();
            km.BeginInit();
            km.DecodePixelWidth = 210;
            km.StreamSource = ks;
            km.CacheOption = BitmapCacheOption.OnLoad;
            km.EndInit();
            ks.Dispose();

            List<string> pwdPa = new List<string> { };
            List<string> pwdFu = new List<string> { };
            string temp = "";
            for (int i = 0; i < str.Length; i++) {
                if (str[i] != '\\') {
                    temp += str[i].ToString();
                } else {
                    bool isDriveRoot = temp.Length == 2 && temp[1] == ':' && char.IsLetter(temp[0]);
                    if (isDriveRoot) {
                        temp += @"\";
                        DriveInfo dr = new DriveInfo(@temp);
                        string[] p0 = ByteToValue(dr.TotalSize);
                        string[] p1 = ByteToValue(dr.TotalFreeSpace);
                        ExtraInfo.Text = $"共 {p0[0]}{p0[1]} 可用 {p1[0]}{p1[1]} ";
                        pwdFu.Add(temp);
                    } else {
                        pwdFu.Add(str.Substring(0, i));
                    }
                    pwdPa.Add(temp);
                    temp = "";
                }
            }
            if (temp != "") {
                bool isDriveRoot = temp.Length == 2 && temp[1] == ':' && char.IsLetter(temp[0]);
                if (isDriveRoot) {
                    temp += @"\";
                }
                pwdPa.Add(temp);
                pwdFu.Add(isDriveRoot ? temp : str.Substring(0));
                temp = "";
            }

            pwdInfo.Children.Clear();
            for (int i = 0; i < pwdPa.Count; i++) {
                System.Windows.Controls.Image pwdItemImg = new System.Windows.Controls.Image {
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 25,
                    Width = 25,
                    Margin = new Thickness(5, 0, 2, 0),
                };

                if (i == 0) {
                    pwdItemImg.Source = km;
                } else {
                    pwdItemImg.Source = bm;
                }

                TextBlock pwdItemText = new TextBlock {
                    Text = pwdPa[i],
                    FontSize = 15,
                    FontWeight = FontWeights.Regular,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                    Foreground = palette.PrimaryText
                };

                int heightOfPwdItem = 34;
                StackPanel t = new StackPanel {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Height = heightOfPwdItem,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 0),
                    Tag = pwdFu[i]
                };
                t.PreviewMouseLeftButtonDown += PwdBarClick;
                ApplyBreadcrumbHoverStyle(t);
                t.Children.Add(pwdItemImg);
                t.Children.Add(pwdItemText);
                t.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double desiredWidth = t.DesiredSize.Width;
                t.Clip = Geometry.Parse(Window_Corner(heightOfPwdItem, t.DesiredSize.Width, 13, 0.0));

                int widthOfIndex = 16;
                if (i != 0) {
                    SvgViewbox s = new SvgViewbox {
                        MaxWidth = 12,
                        MaxHeight = 12,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 5, 0),
                        Source = new Uri("icon/right.svg", UriKind.Relative),
                    };
                    Grid s_parent = new Grid {
                        Height = heightOfPwdItem,
                        Width = widthOfIndex,
                        Clip = Geometry.Parse(Window_Corner(heightOfPwdItem, widthOfIndex, 5, 0.5)),
                        Tag = pwdFu[i - 1]
                    };
                    ApplyBreadcrumbHoverStyle(s_parent);
                    s_parent.Children.Add(s);
                    s_parent.PreviewMouseLeftButtonDown += PathUnfold;
                    pwdInfo.Children.Add(s_parent);
                }
                pwdInfo.Children.Add(t);
            }

            pwd = str;
            DirectoryInfo di = new DirectoryInfo(@str);
            FileInfo[] fics = di.GetFiles();
            var fic = fics.ToList().Where(t => (t.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != (FileAttributes.Hidden | FileAttributes.System)).ToList();
            DirectoryInfo[] dics = di.GetDirectories();
            var dic = dics.ToList().Where(t => (t.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != (FileAttributes.Hidden | FileAttributes.System)).ToList();

            int nF = fic.Count; int nD = dic.Count;

            ExtraInfo.Text += $"共 {nF + nD} 项 已选择 0 项";

            ListInfo.Clear();
            itemsNum = 0;

            string preStr = @pwd;
            if (@pwd == @"C:\" || @pwd == @"D:\" || @pwd == @"E:\") {
                preStr = pwd.Replace(@"\", @"");
            }

            for (int i = 0; i < nF; i++) {
                ListInfo.Add(new MyStruct() { ThumbLoaded = false, X0 = fic[i].Name, X1 = fic[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), X2 = fic[i].Extension.Replace(".", "").ToUpper(), X3 = ByteToValue(fic[i].Length), X4 = fic[i].Length });
            }
            for (int i = 0; i < nD; i++) {
                ListInfo.Add(new MyStruct() { ThumbLoaded = false, X0 = dic[i].Name, X1 = dic[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), X2 = "文件夹", X3 = new string[2] { "", "" }, X4 = -1 });
            }

            itemsNum = nF + nD;

            int slashIndex = (@preStr).LastIndexOf(@"\") + 1;
            CurrentFolder = (@pwd).Substring(slashIndex);

            if (nF + nD > 0) {
                offsetScroll = 0;
                if (timer.IsEnabled) {
                    timer.Stop();
                }
                timer.Start();
            }
        }

        private string[] ByteToValue(long number) {
            string[] sizeSeg = new string[2];
            double last = 1;
            string[] suffixes = new string[] { " B", " KB", " MB", " GB", " TB", " PB" };
            for (int i = 0; i < suffixes.Length; i++) {
                double current = Math.Pow(1024, i + 1);
                double temp = number / current;
                if (temp < 1) {
                    sizeSeg[0] = (number / last).ToString("n2");
                    sizeSeg[1] = suffixes[i];
                    return sizeSeg;
                }
                last = current;
            }
            return sizeSeg;
        }

        private void PwdBarClick(object sender, MouseButtonEventArgs e) {
            CloseBreadcrumbPopup();
            CloseInlineBreadcrumbPopup();
            if (sender is StackPanel s && s.Tag is string fu) {
                if (string.Equals(pwd, fu, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
                Change_ItemSource(fu);
            }
        }

        private void ListView_LostFocus(object sender, RoutedEventArgs e) {
            FILEINFOMATION.UnselectAll();
        }

        private void PathUnfold(object sender, MouseButtonEventArgs e) {
            CloseBreadcrumbPopup();
            CloseInlineBreadcrumbPopup();
            if (sender is Grid clickObject && clickObject.Tag is string fu) {
                if (string.Equals(pwd, fu, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }

                Point point = clickObject.TranslatePoint(new Point(), WorkSpace);
                DirectoryInfo[] subDirs;
                try {
                    subDirs = new DirectoryInfo(fu)
                        .GetDirectories()
                        .Where(dir => (dir.Attributes & FileAttributes.System) == 0)
                        .OrderBy(dir => dir.Name, new NaturalStringComparer())
                        .ToArray();
                } catch {
                    return;
                }

                if (subDirs.Length == 0) {
                    return;
                }

                FileStream fs = new FileStream(@"E:\Repo\wpf\WpfApp2\ico\dif.png", FileMode.Open);
                BitmapImage bm = new BitmapImage();
                bm.BeginInit();
                bm.DecodePixelWidth = 210;
                bm.StreamSource = fs;
                bm.CacheOption = BitmapCacheOption.OnLoad;
                bm.EndInit();
                fs.Dispose();
                bm.Freeze();

                Grid sublist = new Grid {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = palette.PaneBackground
                };

                ScrollViewer scrollViewer = new ScrollViewer {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                    MaxHeight = 198,
                    MaxWidth = 180,
                    Margin = new Thickness(0, 2, 0, 2),
                    Background = palette.PaneBackground
                };
                StackPanel sublistPanel = new StackPanel {
                    Orientation = Orientation.Vertical,
                    Background = palette.PaneBackground
                };
                double itemHeight = 33;

                foreach (var dir in subDirs) {
                    StackPanel sublistItem = new StackPanel {
                        Orientation = Orientation.Horizontal,
                        MaxWidth = 180,
                        Height = itemHeight,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0),
                        Tag = dir.FullName,
                    };

                    System.Windows.Controls.Image pwdItemImg = new System.Windows.Controls.Image {
                        VerticalAlignment = VerticalAlignment.Center,
                        Height = 25,
                        Width = 25,
                        Margin = new Thickness(6, 0, 2, 0),
                        Source = bm
                    };

                    TextBlock pwdItemText = new TextBlock {
                        Text = dir.ToString(),
                        FontSize = 15,
                        FontWeight = FontWeights.Regular,
                        FontFamily = new FontFamily("Microsoft YaHei UI"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0),
                        Foreground = palette.PrimaryText
                    };

                    DataTrigger d = new DataTrigger {
                        Binding = new Binding("IsMouseOver") { Source = sublistItem },
                        Value = true,
                    };
                    d.Setters.Add(new Setter() { Property = BackgroundProperty, Value = palette.BreadcrumbHoverBackground });
                    Style st = new Style();
                    st.Setters.Add(new Setter() { Property = BackgroundProperty, Value = palette.PaneBackground });
                    st.Triggers.Add(d);
                    sublistItem.Style = st;
                    sublistItem.Children.Add(pwdItemImg);
                    sublistItem.Children.Add(pwdItemText);
                    sublistItem.PreviewMouseLeftButtonDown += PwdBarClick;
                    sublistPanel.Children.Add(sublistItem);
                }

                scrollViewer.Content = sublistPanel;
                System.Windows.Shapes.Path backgroundPath = new System.Windows.Shapes.Path {
                    //Stroke = palette.DividerBrush,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                };
                sublist.Children.Add(scrollViewer);

                sublist.SizeChanged += (s, _) => {
                    if (sublist.ActualWidth <= 0 || sublist.ActualHeight <= 0) return;
                    sublist.Clip = Geometry.Parse(Window_Corner(sublist.ActualHeight, sublist.ActualWidth, squircle_radius, 0.5));
                    sublist.Margin = new Thickness(point.X, point.Y - sublist.ActualHeight + 1, 0, 0);
                    backgroundPath.Data = Geometry.Parse(Window_Corner(sublist.ActualHeight, sublist.ActualWidth, squircle_radius, 1));
                    backgroundPath.Stroke = palette.DividerBrush;
                    sublist.Children.Add(backgroundPath);
                    for (int j = 0; j < sublistPanel.Children.Count; j++) {
                        if (sublistPanel.Children[j] is StackPanel p) {
                            p.Clip = Geometry.Parse(Window_Corner(itemHeight, sublist.ActualWidth - 4, squircle_radius - 2, 1));
                        }
                    }                    
                };

                WorkSpace.Children.Add(sublist);
                isSublistExist = sublist;
            }
        }

        private void BuildBreadcrumbPopup(IEnumerable<DirectoryInfo> subDirs) {
            if (BreadcrumbPopupPanel == null) {
                return;
            }

            BreadcrumbPopupPanel.Children.Clear();

            foreach (var dir in subDirs) {
                StackPanel item = new StackPanel {
                    Orientation = Orientation.Horizontal,
                    MaxWidth = BreadcrumbPopupWidth - 12,
                    Width = BreadcrumbPopupWidth - 12,
                    Height = BreadcrumbItemHeight,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 2, 2, 0),
                    Tag = dir.FullName,
                    Background = Brushes.Transparent
                };
                item.SizeChanged += BreadcrumbPopupItem_SizeChanged;

                SvgViewbox icon = new SvgViewbox {
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 20,
                    Width = 20,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(6, 0, 6, 0),
                    Source = new Uri("/icon/folder.svg", UriKind.Relative),
                };

                TextBlock label = new TextBlock {
                    Text = dir.Name,
                    FontSize = 15,
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                    Foreground = palette.PrimaryText,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                ApplyBreadcrumbHoverStyle(item);
                item.Children.Add(icon);
                item.Children.Add(label);
                item.PreviewMouseLeftButtonDown += BreadcrumbPopupItem_Click;
                BreadcrumbPopupPanel.Children.Add(item);
            }

            Dispatcher.BeginInvoke(new Action(RefreshBreadcrumbPopupChrome), DispatcherPriority.Background);
        }

        private void BreadcrumbPopupItem_Click(object sender, MouseButtonEventArgs e) {
            if (sender is FrameworkElement fe && fe.Tag is string target) {
                e.Handled = true;
                CloseBreadcrumbPopup();
                Change_ItemSource(target);
            }
        }

        private void BreadcrumbPopupItem_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (sender is FrameworkElement fe) {
                double width = e.NewSize.Width > 0 ? e.NewSize.Width : BreadcrumbPopupWidth - 8;
                double height = e.NewSize.Height > 0 ? e.NewSize.Height : BreadcrumbItemHeight;
                fe.Clip = Geometry.Parse(Window_Corner(height, width, squircle_radius - 3, 0.75));
            }
        }

        private void ApplyThemeToBreadcrumbPopup() {
            if (BreadcrumbPopupBorder == null) return;
            BreadcrumbPopupBorder.Background = palette.PaneBackground;
            BreadcrumbPopupBorder.BorderBrush = palette.DividerBrush;
            BreadcrumbScrollViewer.Background = palette.PaneBackground;
            if (BreadcrumbPopupBorder.Effect == null) {
                BreadcrumbPopupBorder.Effect = new DropShadowEffect {
                    Color = Colors.Black,
                    Direction = 315,
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.18
                };
            }
        }

        private void RefreshBreadcrumbPopupChrome() {
            if (BreadcrumbPopupBorder == null) return;
            double height = BreadcrumbPopupBorder.ActualHeight;
            double width = BreadcrumbPopupBorder.ActualWidth;
            if (height <= 0 || width <= 0) return;

            BreadcrumbPopupBorder.Clip = Geometry.Parse(Window_Corner(height, width, squircle_radius, 0.75));

            if (BreadcrumbPopupPanel != null) {
                foreach (var child in BreadcrumbPopupPanel.Children.OfType<FrameworkElement>()) {
                    double childWidth = child.ActualWidth > 0 ? child.ActualWidth : width - 8;
                    double childHeight = child.ActualHeight > 0 ? child.ActualHeight : BreadcrumbItemHeight;
                    child.Clip = Geometry.Parse(Window_Corner(childHeight, childWidth, squircle_radius - 3, 0.75));
                }
            }
        }

        private void UpdateBreadcrumbPopupOffset() {
            if (BreadcrumbPopup == null || BreadcrumbPopupBorder == null || breadcrumbAnchor == null) {
                return;
            }

            double popupWidth = BreadcrumbPopupBorder.ActualWidth > 0 ? BreadcrumbPopupBorder.ActualWidth : BreadcrumbPopupWidth;
            double anchorWidth = breadcrumbAnchor.ActualWidth > 0 ? breadcrumbAnchor.ActualWidth : 0;
            BreadcrumbPopup.HorizontalOffset = (popupWidth - anchorWidth) * -0.5;
        }

        private void Item_MouseDoubleClickForListView(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount != 2) {
                return;
            }
            System.Windows.Controls.ListViewItem temp = sender as System.Windows.Controls.ListViewItem;
            MyStruct myStruct = temp.DataContext as MyStruct;
            string str;
            string strType;
            if (myStruct != null) {
                if (pwd == @"C:\" || pwd == @"D:\" || pwd == @"E:\") {
                    str = $@"{pwd}{myStruct.X0}";
                } else {
                    str = $@"{pwd}\{myStruct.X0}";
                }
                strType = myStruct.X2;
            } else {
                Grid s = sender as Grid;
                TextBlock t = s.Children[1] as TextBlock;
                if (pwd == @"C:\" || pwd == @"D:\" || pwd == @"E:\") {
                    str = $@"{pwd}{t.Text}";
                } else {
                    str = $@"{pwd}\{t.Text}";
                }
                strType = t.Tag as string;
            }

            if (strType == "文件夹") {
                Change_ItemSource(@str);
            } else {
                Process.Start(@str);
            }
        }

        private Grid recordGrid = null;
        private void Item_MouseClickForGrid(object sender, MouseButtonEventArgs e) {
            Grid temp = sender as Grid;

            if (e.ClickCount == 1) {
                recordGrid?.ClearValue(BackgroundProperty);
                temp.Background = palette.ListSelectedBackground;
                recordGrid = temp;
            }

            if (e.ClickCount != 2) {
                return;
            }

            string str;
            string strType;
            if (temp.DataContext is MyStruct myStruct) {
                if (pwd == @"C:\" || pwd == @"D:\" || pwd == @"E:\") {
                    str = $@"{pwd}{myStruct.X0}";
                } else {
                    str = $@"{pwd}\{myStruct.X0}";
                }
                strType = myStruct.X2;
            } else {
                Grid s = sender as Grid;
                TextBlock t = s.Children[1] as TextBlock;
                if (pwd == @"C:\" || pwd == @"D:\" || pwd == @"E:\") {
                    str = $@"{pwd}{t.Text}";
                } else {
                    str = $@"{pwd}\{t.Text}";
                }
                strType = t.Tag as string;
            }

            if (strType == "文件夹") {
                Change_ItemSource(@str);
            } else {
                Process.Start(@str);
            }
        }
    }
}
