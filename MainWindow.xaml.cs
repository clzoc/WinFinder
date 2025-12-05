using Microsoft.WindowsAPICodePack.Shell;
using SharpVectors.Converters;
using SharpVectors.Dom;
using SharpVectors.Dom.Events;
using SharpVectors.Dom.Svg;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Drawing.Configuration;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.CustomMarshalers;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using File = System.IO.File;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;


namespace WinFinder {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    ///     
    public partial class MainWindow : Window, INotifyPropertyChanged {

        public MainWindow() {
            InitializeComponent();

            timer.Tick += Timer_Tick;
            SizeChanged += new SizeChangedEventHandler(MainWindow_Resize);

            ListInfo.CollectionChanged += ListInfo_CollectionChanged;
            foreach (var item in ListInfo) {
                item.PropertyChanged += Item_PropertyChanged;
            }
            FILEINFOMATION.ItemsSource = ListInfo;
            GridViewContainer.ItemsSource = ListInfo;

            ApplyNaturalSort("X0", ListSortDirection.Ascending);


            // Add global click handler
            PreviewMouseLeftButtonDown += (s, args) => {
                if (isSublistExist != null &&
                    !isSublistExist.IsMouseOver) {
                    CloseSublist(null, null);
                }             
            };

            //// 初始化自动滚动定时器
            _autoScrollTimer = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;
        }        

        private void PwdInfoScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        // 添加选中项计数属性
        private int _selectedCount = 0;
        public int SelectedCount {
            get => _selectedCount;
            set {
                _selectedCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedCount"));
                UpdateExtraInfo();
            }
        }

        private void ListInfo_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            // 处理新增项
            if (e.NewItems != null) {
                foreach (MyStruct item in e.NewItems) {
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            // 处理移除项
            if (e.OldItems != null) {
                foreach (MyStruct item in e.OldItems) {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            UpdateSelectionCount();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "IsSelected") {
                UpdateSelectionCount();
            }
        }

        private void UpdateSelectionCount() {
            SelectedCount = ListInfo.Count(item => item.IsSelected);
        }

        private void UpdateExtraInfo() {
            DirectoryInfo di = new DirectoryInfo(@pwd);
            if (DriveInfo.GetDrives().Any(d => d.Name == di.Root.FullName)) {
                DriveInfo drive = new DriveInfo(di.Root.FullName);
                string[] p0 = ByteToValue(drive.TotalSize);
                string[] p1 = ByteToValue(drive.TotalFreeSpace);
                ExtraInfo.Text = $"共 {p0[0]}{p0[1]} 可用 {p1[0]}{p1[1]} ";
            }            
            ExtraInfo.Text += $"共 {ListInfo.Count} 项 已选择 {SelectedCount} 项";
        }

        // 原有代码 
        public DispatcherTimer timer = new DispatcherTimer {
            Interval = new TimeSpan(0, 0, 0, 0, 50)
        };

        public event PropertyChangedEventHandler PropertyChanged;

        private void MainWindow_Resize(object sender, EventArgs e) {
            Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
            ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth - 12, squircle_radius - 3, 0.25);
        }

        // Box selection variables
        private Point selectionStart;
        private Point _selectionEndPoint; // Add this variable
        private bool isSelecting;
        private Grid selectionArea = new Grid();

        private void ContentGrid_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) return;

            // Only start selection if the click is on the grid background, not an item
            if (!(e.OriginalSource is FrameworkElement fe) || fe.DataContext == null || fe.DataContext is MyStruct) {
                selectionStart = e.GetPosition(ContentArea);
                _selectionEndPoint = selectionStart; // Initialize the end point
                isSelecting = true;

                // Initialize selection area
                selectionArea = new Grid {
                    Background = new SolidColorBrush(Color.FromArgb(127, 154, 186, 232)),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                ContentArea.CaptureMouse();
                ContentArea.Children.Add(selectionArea);

                // Clear previous selection if Ctrl is not held down
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) {
                    foreach (MyStruct item in ListInfo.Where(i => i.IsSelected)) {
                        item.IsSelected = false;
                    }
                }
            }
        }

        // 添加这些成员变量
        private DispatcherTimer _autoScrollTimer;
        private double _autoScrollSpeed;
        private Point _lastMousePosition;
        private const double ScrollEdgeThreshold = 30; // 边缘滚动阈值
        private const double ScrollSpeedFactor = 5.0; // 滚动速度因子

        private void ContentGrid_MouseMove(object sender, MouseEventArgs e) {            
            if (!isSelecting) return;

            Trace.WriteLine("multiple thin");

            _lastMousePosition = e.GetPosition(ContentArea); // We still need this for auto-scroll logic
            _selectionEndPoint = _lastMousePosition; // Update the end point with the physical mouse position

            UpdateSelectionBox(); // Call the new helper method

            // The rest of your auto-scroll logic remains the same
            double scrollRef = 0;
            bool shouldScroll = false;
            if (_lastMousePosition.Y > ContentArea.ActualHeight - ScrollEdgeThreshold) {
                scrollRef = _lastMousePosition.Y - (ContentArea.ActualHeight - ScrollEdgeThreshold);
                shouldScroll = true;
            } else if (_lastMousePosition.Y < ScrollEdgeThreshold) {
                scrollRef = _lastMousePosition.Y - ScrollEdgeThreshold;
                shouldScroll = true;
            }

            _autoScrollSpeed = scrollRef * ScrollSpeedFactor;

            if (shouldScroll) {
                if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
            } else {
                if (_autoScrollTimer.IsEnabled) _autoScrollTimer.Stop();
            }

            // Update the actual item selection
            UpdateSelectedItems();
        }

        // NEW HELPER METHOD
        private void UpdateSelectionBox() {
            // Calculate rect geometry based on the start and end points
            double left = Math.Min(selectionStart.X, _selectionEndPoint.X);
            double top = Math.Min(selectionStart.Y, _selectionEndPoint.Y);
            double width = Math.Abs(selectionStart.X - _selectionEndPoint.X);
            double height = Math.Abs(selectionStart.Y - _selectionEndPoint.Y);

            // Position and size the visual rectangle
            selectionArea.Margin = new Thickness(left, top, 0, 0);
            selectionArea.Width = width;
            selectionArea.Height = height;

            // Update the clip geometry
            if (selectionArea.ActualWidth > 0 && selectionArea.ActualHeight > 0) {
                selectionArea.Clip = Geometry.Parse(Window_Corner(selectionArea.ActualHeight, selectionArea.ActualWidth,
                    Math.Min(Math.Min(selectionArea.ActualHeight * 0.3, selectionArea.ActualWidth * 0.3), squircle_radius), 1));
            }
        }

        // NEW HELPER METHOD (replaces your old SelectItemsInListView)
        private void UpdateSelectedItems() {
            ListView activeListView = FILEINFOMATION.Visibility == Visibility.Visible ? FILEINFOMATION : GridViewContainer;

            // The selection rect is now defined by the visual 'selectionArea'
            Rect selectionRect = new Rect(selectionArea.Margin.Left, selectionArea.Margin.Top, selectionArea.Width, selectionArea.Height);

            // This part of your logic is good, but let's make it work with the IsSelected property
            // on your data item for better virtualization support.
            foreach (MyStruct item in activeListView.ItemsSource) {
                var container = activeListView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;

                // If container is null, the item is virtualized (not visible). We can't check it now.
                if (container != null) {
                    Point itemTopLeft = container.TranslatePoint(new Point(0, 0), ContentArea);
                    Rect itemRect = new Rect(itemTopLeft, new Size(container.ActualWidth, container.ActualHeight));

                    // Check if item intersects with selection area
                    if (selectionRect.IntersectsWith(itemRect)) {
                        if (!item.IsSelected) item.IsSelected = true;
                    } else {
                        // Only deselect if Ctrl is not held
                        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) {
                            if (item.IsSelected) item.IsSelected = false;
                        }
                    }
                }
            }
            // UpdateSelectionCount() will be called automatically by the PropertyChanged event.
        }

        // 获取 ListView 的 ScrollViewer
        private ScrollViewer GetScrollViewer(DependencyObject depObj) {
            if (depObj is ScrollViewer) return depObj as ScrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }        

        private void AutoScrollTimer_Tick(object sender, EventArgs e) {
            if (!isSelecting) {
                _autoScrollTimer.Stop();
                return;
            }

            ListView activeListView = FILEINFOMATION.Visibility == Visibility.Visible ? FILEINFOMATION : GridViewContainer;
            ScrollViewer scrollViewer = GetScrollViewer(activeListView);
            if (scrollViewer == null) return;

            // Calculate the scroll amount for this tick
            double scrollAmount = _autoScrollSpeed * (_autoScrollTimer.Interval.TotalSeconds);
            double newOffset = scrollViewer.VerticalOffset + scrollAmount;

            // Clamp the new offset within valid bounds
            newOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, newOffset));

            // Determine the actual distance scrolled (might be less than scrollAmount at the edges)
            double actualScrolledDistance = newOffset - scrollViewer.VerticalOffset;

            // If we didn't actually scroll, do nothing.
            if (Math.Abs(actualScrolledDistance) < 1) return;

            // Apply the scroll
            scrollViewer.ScrollToVerticalOffset(newOffset);

            // *** THE KEY CHANGE: Extend the selection endpoint programmatically ***

            selectionStart.Y -= actualScrolledDistance;
            // _selectionEndPoint.Y += actualScrolledDistance;

            // Now, update the visual rectangle to match the new logical endpoint
            UpdateSelectionBox();

            // Finally, re-evaluate which items are inside the new, larger rectangle
            UpdateSelectedItems();
        }

        private void ContentGrid_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!isSelecting || selectionArea == null) return;

            ContentArea.ReleaseMouseCapture();
            isSelecting = false;

            // 停止自动滚动定时器
            if (_autoScrollTimer.IsEnabled) {
                _autoScrollTimer.Stop();
            }

            ContentArea.Children.Remove(selectionArea);
        }

        private void ContentArea_MouseWheel(object sender, MouseWheelEventArgs e) {
            // First, check if we are in the middle of a selection.
            // We also need to ensure we have a valid ScrollViewer to command.
            if (!isSelecting) {
                // If not, do nothing and let the event process normally (though it won't do much with capture on).
                return;
            }

            // Manually scroll the active ScrollViewer.
            // The e.Delta value is positive for scrolling up and negative for scrolling down.
            // Scrolling up should decrease the VerticalOffset, so we subtract the Delta.
            // _activeScrollViewer.ScrollToVerticalOffset(_activeScrollViewer.VerticalOffset - e.Delta);

            ListView activeListView = FILEINFOMATION.Visibility == Visibility.Visible ? FILEINFOMATION : GridViewContainer;
            ScrollViewer scrollViewer = GetScrollViewer(activeListView);
            if (scrollViewer == null) return;            

            double newOffset = scrollViewer.VerticalOffset - e.Delta;

            // Clamp the new offset within valid bounds
            newOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, newOffset));

            // Determine the actual distance scrolled (might be less than scrollAmount at the edges)
            double actualScrolledDistance = newOffset - scrollViewer.VerticalOffset;

            // If we didn't actually scroll, do nothing.
            if (Math.Abs(actualScrolledDistance) < 1) return;

            scrollViewer.ScrollToVerticalOffset(newOffset);

            selectionStart.Y -= actualScrolledDistance;
            // _selectionEndPoint.Y += actualScrolledDistance;

            // Now, update the visual rectangle to match the new logical endpoint
            UpdateSelectionBox();

            // Finally, re-evaluate which items are inside the new, larger rectangle
            UpdateSelectedItems();

            // IMPORTANT: After any programmatic scroll, the visual items have moved.
            // We must update the selection rectangle's position relative to the newly scrolled items
            // and re-evaluate which items are selected.
            // The easiest way to do this is to re-run the logic from our MouseMove handler.

            // We create a "fake" MouseEventArgs to simulate a mouse move at the last known position.
            // This triggers all our existing update logic.
            //MouseEventArgs fakeArgs = new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) {
            //    RoutedEvent = Mouse.MouseMoveEvent
            //};
            //ContentGrid_MouseMove(this, fakeArgs); // This re-uses existing logic perfectly.

            // Mark the event as handled to prevent it from bubbling up and causing any unintended side effects.
            e.Handled = true;
        }

        private readonly string[] icon = { "/icon/Maximize_Button_Hover_M.svg", "/icon/Maximize_Button_Hover_Zoom_M.svg" };

        private static readonly double squircle_radius = 15;

        private string info = "";
        public string Info {
            get {
                return info;
            }
            set {
                info = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Info"));
            }
        }

        private string clipInfo = "";
        public string ClipInfo {
            get {
                return clipInfo;
            }
            set {
                clipInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ClipInfo"));
            }
        }

        private string sideclipInfo = "";
        public string SideClipInfo {
            get {
                return sideclipInfo;
            }
            set {
                sideclipInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SideClipInfo"));
            }
        }

        private string gridClipInfo = "";
        public string GridClipInfo {
            get {
                return gridClipInfo;
            }
            set {
                gridClipInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("GridClipInfo"));
            }
        }

        private string gridInnerClipInfo = "";
        public string GridInnerClipInfo {
            get {
                return gridInnerClipInfo;
            }
            set {
                gridInnerClipInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("GridInnerClipInfo"));
            }
        }

        private string currentFolder = "";
        public string CurrentFolder {
            get {
                return currentFolder;
            }
            set {
                currentFolder = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentFolder"));
            }
        }

        private int isZoom = 0;

        private string zoomButton = "/icon/Maximize_Button_Hover_M.svg";
        public string ZoomButton {
            get {
                return zoomButton;
            }
            set {
                zoomButton = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ZoomButton"));
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
            //Trace.WriteLine($"For Debug Information {end:0.00}");
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
                ZoomButton = icon[isZoom];

                //Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
                //ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 11, 0.5);
            } else {
                double h = Height;
                double w = Width;
                Height = 900 + 2;
                Width = 1200 + 2;
                Top = 0.5 * (h - Height);
                Left = 0.5 * (w - Width);
                isZoom = 0;
                ZoomButton = icon[isZoom];

                //Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
                //ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 11, 0.5);
            }
        }



        public ObservableCollection<MyStruct> ListInfo = new ObservableCollection<MyStruct>();

        private List<string> sidePath = new List<string> { @"C:\Users\tsunami", @"C:\Users\tsunami\Desktop", @"C:\Users\tsunami\Downloads", @"C:\Users\tsunami\Music", @"C:\Users\tsunami\Pictures", @"C:\Users\tsunami\Videos", @"C:\Users\tsunami\Documents", };

        private double fileHeight = 30;
        private double gridHeight = 150;
        private void ContentView(object sender, RoutedEventArgs e) {
            GridClipInfo = Window_Corner(gridHeight, 115, squircle_radius, 0.5);
            GridInnerClipInfo = Window_Corner(gridHeight, 115, squircle_radius, 0.5);

            List<string> side = new List<string> { "tsunami", "桌面", "下载", "音乐", "图片", "视频", "文稿", };
            List<string> icon = new List<string> {
                "/icon/house.svg",
                "/icon/pc.svg",
                "/icon/icloud.and.arrow.down.svg",
                "/icon/headphones.svg",
                "/icon/camera.svg",
                "/icon/film.svg",
                "/icon/doc.text.svg",
            };
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo item in drives) {
                sidePath.Add(item.Name);
                string place = "";
                if (item.DriveType.ToString() == "Fixed") {
                    place = "盘";
                }
                side.Add($"{place}({item.Name})");
                icon.Add("/icon/internaldrive.svg");
            }

            Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
            ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth - 12, squircle_radius - 3, 0.25);

            //currentFolderRegion.Clip = Geometry.Parse(Window_Corner(35, 325, 10, 0.5));

            PathBack.Height = 38;
            PathBack.Width = 38;
            PathBack.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            PathBack.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            PathBack.Clip = Geometry.Parse(Window_Corner(38, 38, 11, 0.0));

            PathMove.Height = 38;
            PathMove.Width = 38;
            PathMove.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            PathMove.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            PathMove.Clip = Geometry.Parse(Window_Corner(38, 38, 11, 0.0));

            ViewListView.Height = 38;
            ViewListView.Width = 38;
            ViewListView.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ViewListView.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            ViewListView.Clip = Geometry.Parse(Window_Corner(38, 38, 11, 0.0));

            ViewGridView.Height = 38;
            ViewGridView.Width = 38;
            ViewGridView.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ViewGridView.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            ViewGridView.Clip = Geometry.Parse(Window_Corner(38, 38, 11, 0.0));

            double sideitemheight = 40;
            SideClipInfo = Window_Corner(sideitemheight, SideBar.ActualWidth, sideitemheight * 0.35, 0.5);

            SideBar.Tag = 0;

            for (int i = 0; i < side.Count; i++) {
                Grid t = new Grid {
                    Height = sideitemheight,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                };
                System.Windows.Data.Binding m = new System.Windows.Data.Binding("Data") {
                    Source = SideClip
                };
                t.SetBinding(ClipProperty, m);
                t.Tag = sidePath[i];
                t.PreviewMouseLeftButtonDown += DiskHandler;

                DataTrigger d = new DataTrigger {
                    Binding = new System.Windows.Data.Binding("IsMouseOver") { Source = t },
                    Value = true,
                };
                d.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")) });
                Style st = new Style();
                st.Triggers.Add(d);
                t.Style = st;

                if (i == 0) {
                    t.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8"));
                }

                SvgViewbox s;

                s = new SvgViewbox {
                    MaxWidth = t.Height - 15,
                    MaxHeight = t.Height - 15,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 100, 0),
                    Source = new Uri(icon[i], UriKind.Relative),
                };

                TextBlock textBlock = new TextBlock {
                    Text = side[i],
                    FontSize = 21,
                    FontWeight = FontWeights.Regular,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(70, 0, 0, 0),
                };

                _ = t.Children.Add(s);
                _ = t.Children.Add(textBlock);

                _ = SideBar.Children.Add(t);
            }

            Change_ItemSource(@"C:\Users\tsunami");
        }


        private void DiskHandler(object sender, RoutedEventArgs e) {
            Grid g = sender as Grid;
            string s = g.Tag as string;
            int gIndex = SideBar.Children.IndexOf(g);
            int cIndex = (int)SideBar.Tag;
            if (s != @pwd) {
                Change_ItemSource(@s);
                if (gIndex != cIndex) {
                    g.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8"));
                    if (cIndex != -1) {
                        Grid c = SideBar.Children[cIndex] as Grid;
                        c.ClearValue(BackgroundProperty);
                    }
                    SideBar.Tag = gIndex;
                }
            }
            return;
        }

        private static readonly string[] suffixes = new string[] { " B", " KB", " MB", " GB", " TB", " PB" };
        public string[] ByteToValue(long number) {
            string[] sizeSeg = new string[2];
            double last = 1;
            for (int i = 0; i < suffixes.Length; i++) {
                double current = Math.Pow(1024, i + 1);
                double temp = number / current;
                if (temp < 1) {
                    sizeSeg[0] = (number / last).ToString("n2");
                    sizeSeg[1] = suffixes[i];
                    return sizeSeg;
                    // return (number / last).ToString("n2") + suffixes[i];
                }
                last = current;
            }
            return sizeSeg;
        }

        private void Window_Back(object sender, RoutedEventArgs e) {
            DirectoryInfo di = new DirectoryInfo(@pwd);

            if (pwd != @"C:\" && pwd != @"D:\" && pwd != @"E:\") {
                string next = di.Parent.FullName;
                int index = sidePath.IndexOf(@next);
                once = pwd;
                if (index != -1) {
                    RoutedEventArgs o = new RoutedEventArgs();
                    DiskHandler(SideBar.Children[index], o);
                } else {
                    Change_ItemSource(@next);
                }
            } else {
                return;
            }
        }

        private void Window_Retu(object sender, RoutedEventArgs e) {
            if (once == "") {
                return;
            }
            int index = sidePath.IndexOf(@once);
            if (index != -1) {
                RoutedEventArgs o = new RoutedEventArgs();
                DiskHandler(SideBar.Children[index], o);
            } else {
                Change_ItemSource(@once);
            }
            return;
        }

        private void Change_ItemSource(string str) {
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
                    if (temp == "C:" || temp == "D:" || temp == "E:") {
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
                if (temp == "C:" || temp == "D:" || temp == "E:") {
                    temp += @"\";
                }
                pwdPa.Add(temp);
                temp = "";
                pwdFu.Add(str.Substring(0));
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
                    // Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("Transparent")),
                    Margin = new Thickness(0, 0, 6, 0),
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

                DataTrigger d = new DataTrigger {
                    Binding = new System.Windows.Data.Binding("IsMouseOver") { Source = t },
                    Value = true,
                };
                d.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")) });
                Style st = new Style();
                st.Triggers.Add(d);
                t.Style = st;
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
                    DataTrigger a = new DataTrigger {
                        Binding = new System.Windows.Data.Binding("IsMouseOver") { Source = s_parent },
                        Value = true,
                    };
                    a.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")) });
                    Style ht = new Style();
                    ht.Triggers.Add(a);
                    s_parent.Style = ht;
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
                //if (GridViewContainer.IsVisible == false) {
                //    FILEINFOMATION.ScrollIntoView(FILEINFOMATION.Items[0]);
                //} else {
                //    ScrollViewer scrollViewer = VisualTreeHelper.GetChild(GridViewContainer, 0) as ScrollViewer;
                //    scrollViewer.ScrollToTop();
                //}
                offsetScroll = 0;
                if (timer.IsEnabled) {
                    timer.Stop();
                }
                timer.Start();
            }
        }

        private void PwdBarClick(object sender, MouseButtonEventArgs e) {
            if (isSublistExist != null) {
                WorkSpace.Children.Remove(isSublistExist);
                isSublistExist = null;
            }
            StackPanel s = sender as StackPanel;
            string fu = s.Tag as string;
            if (@pwd == fu) {
                return;
            }
            Change_ItemSource(@fu);
        }

        //private Popup folderPopup = null; // 添加为类成员变量

        //private void PathUnfold(object sender, MouseButtonEventArgs e) {
        //    Grid g = sender as Grid;
        //    string fu = g.Tag as string;
        //    if (@pwd == fu) {
        //        return;
        //    }

        //    // 关闭已存在的弹出窗口
        //    if (folderPopup != null && folderPopup.IsOpen) {
        //        folderPopup.IsOpen = false;
        //    }

        //    // 获取目标路径的子文件夹
        //    DirectoryInfo di = new DirectoryInfo(fu);
        //    DirectoryInfo[] subDirs;
        //    try {
        //        subDirs = di.GetDirectories()
        //            .Where(dir => (dir.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
        //            .ToArray();
        //    } catch {
        //        return; // 无权限访问等异常处理
        //    }

        //    // 创建ListView展示子文件夹
        //    System.Windows.Controls.ListView folderList = new System.Windows.Controls.ListView {
        //        Background = System.Windows.Media.Brushes.White,
        //        BorderThickness = new Thickness(1),
        //        BorderBrush = System.Windows.Media.Brushes.LightGray,
        //        MaxHeight = 300,
        //        Width = 200,
        //        ItemsSource = subDirs.Select(d => d.Name).ToList()
        //    };

        //    // 设置ListView样式
        //    Style itemStyle = new Style(typeof(System.Windows.Controls.ListViewItem));
        //    itemStyle.Setters.Add(new Setter(BackgroundProperty, System.Windows.Media.Brushes.White));
        //    itemStyle.Setters.Add(new Setter(ForegroundProperty, System.Windows.Media.Brushes.Black));
        //    itemStyle.Setters.Add(new Setter(FontSizeProperty, 14.0));
        //    itemStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(8, 4, 8, 4)));
        //    folderList.ItemContainerStyle = itemStyle;          

        //    // 创建Popup容器
        //    Border popupContainer = new Border {
        //        //Background = System.Windows.Media.Brushes.White,
        //        //BorderBrush = System.Windows.Media.Brushes.LightGray,
        //        //BorderThickness = new Thickness(1),
        //        //CornerRadius = new CornerRadius(5),
        //        Child = folderList,
        //    };

        //    popupContainer.SizeChanged += (s, f) => {
        //        if (popupContainer.ActualWidth > 0 && popupContainer.ActualHeight > 0) {
        //            popupContainer.Clip = Geometry.Parse(Window_Corner(popupContainer.ActualHeight, popupContainer.ActualWidth, squircle_radius, 1));
        //        }
        //    };

        //    //Grid popupContainer = new Grid {
        //    //    Width = 150,
        //    //    Height = 300,
        //    //    Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")),
        //    //    Clip = Geometry.Parse(Window_Corner(300, 150, squircle_radius, 1))
        //    //};

        //    //System.Windows.Shapes.Path popupContainer = new System.Windows.Shapes.Path {
        //    //    Stroke = System.Windows.Media.Brushes.Black,
        //    //    StrokeThickness = 1,
        //    //    Data = Geometry.Parse(Window_Corner(300, 150, squircle_radius, 1)),
        //    //};

        //    //// 创建主容器（Grid）
        //    //Grid popupContainer = new Grid {
        //    //    Width = 200,  // 宽度匹配ListView
        //    //    Height = 300, // 高度匹配ListView
        //    //    Clip = Geometry.Parse(Window_Corner(304, 204, squircle_radius, 1)) // 裁剪为相同形状
        //    //};

        //    //// 创建Path作为背景（可选）
        //    //System.Windows.Shapes.Path backgroundPath = new System.Windows.Shapes.Path {
        //    //    Stroke = System.Windows.Media.Brushes.Black,
        //    //    StrokeThickness = 1,
        //    //    // Fill = System.Windows.Media.Brushes.White, // 添加背景色
        //    //    Data = Geometry.Parse(Window_Corner(304, 204, squircle_radius, 1.5))
        //    //};
        //    //popupContainer.Children.Add(backgroundPath); // 先添加背景                                                        
        //    //popupContainer.Children.Add(folderList);                                                        

        //    // 创建Popup
        //    folderPopup = new Popup {
        //        Child = popupContainer,
        //        Placement = PlacementMode.Bottom,
        //        PlacementTarget = g,
        //        //StaysOpen = false, // 点击外部自动关闭
        //        StaysOpen = true,
        //        AllowsTransparency = true
        //    };

        //    // 处理文件夹选择
        //    folderList.MouseDoubleClick += (s, args) => {
        //        if (folderList.SelectedItem != null) {
        //            string selectedFolder = System.IO.Path.Combine(fu, folderList.SelectedItem.ToString());
        //            folderPopup.IsOpen = false;
        //            Change_ItemSource(selectedFolder);
        //        }
        //    };

        //    // 显示Popup
        //    folderPopup.IsOpen = true;
        //}

        private void ListView_LostFocus(object sender, RoutedEventArgs e) {
            //if (sender is System.Windows.Controls.ListView listView) {
            //    listView.UnselectAll();
            //}
            FILEINFOMATION.UnselectAll();
        }

        private void CloseSublist(object sender, RoutedEventArgs e) {
            if (isSublistExist != null) {
                WorkSpace.Children.Remove(isSublistExist);
                isSublistExist = null;
            }
        }

        Grid isSublistExist = null;
        private void PathUnfold(object sender, MouseButtonEventArgs e) {
            if (isSublistExist != null) {
                WorkSpace.Children.Remove(isSublistExist);
                isSublistExist = null;
            }
            // System.Windows.Point point = e.GetPosition(WorkSpace);
            Grid clickObject = sender as Grid;
            Point point = clickObject.TranslatePoint(new Point(), WorkSpace);

            Grid g = sender as Grid;
            string fu = g.Tag as string;
            if (@pwd == fu) {
                return;
            }
            DirectoryInfo di = new DirectoryInfo(fu);
            DirectoryInfo[] subDirs;
            try {
                subDirs = di.GetDirectories()
                    //.Where(dir => (dir.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .Where(dir => (dir.Attributes & (FileAttributes.System)) == 0)
                    .ToArray();
            } catch {
                return; // 无权限访问等异常处理
            }

            // 使用自然排序对文件夹进行排序
            Array.Sort(subDirs, new NaturalStringComparer());  // 这里对文件夹按名字进行自然排序

            Grid sublist = new Grid {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF")),
                //Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F5F5")),
                // Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")),
            };

            //StackPanel sublistPanel = new StackPanel {
            //    Orientation = System.Windows.Controls.Orientation.Vertical,
            //    MaxWidth = 180,
            //    MaxHeight = 200,
            //    Margin = new Thickness(0, 0, 0, 3),
            //};

            ScrollViewer scrollViewer = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 198,
                MaxWidth = 180,
                Margin = new Thickness(0, 2, 0, 2),                
            };
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            StackPanel sublistPanel = new StackPanel {
                Orientation = System.Windows.Controls.Orientation.Vertical
            };

            FileStream fs = new FileStream(@"E:\Repo\wpf\WpfApp2\ico\dif.png", FileMode.Open);
            BitmapImage bm = new BitmapImage();
            bm.BeginInit();
            bm.DecodePixelWidth = 210;
            bm.StreamSource = fs;
            bm.CacheOption = BitmapCacheOption.OnLoad;
            bm.EndInit();
            fs.Dispose();
            bm.Freeze();

            double itemHeight = 33;
            for (int i = 0; i < subDirs.Length; i++) {
                StackPanel sublistItem = new StackPanel {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    MaxWidth = 180,
                    Height = itemHeight,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0),
                    Tag = subDirs[i].FullName,
                };
                
                System.Windows.Controls.Image pwdItemImg = new System.Windows.Controls.Image {
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 25,
                    Width = 25,
                    Margin = new Thickness(6, 0, 2, 0),
                };
                pwdItemImg.Source = bm;

                TextBlock pwdItemText = new TextBlock {
                    Text = subDirs[i].ToString(),
                    FontSize = 15,
                    FontWeight = FontWeights.Regular,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    // Foreground = System.Windows.Media.Brushes.Black,
                    // Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("Transparent")),
                    Margin = new Thickness(0, 0, 6, 0),
                };

                DataTrigger d = new DataTrigger {
                    Binding = new System.Windows.Data.Binding("IsMouseOver") { Source = sublistItem },
                    Value = true,
                };                
                d.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")) });
                Style st = new Style();
                st.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("Transparent")) });
                st.Triggers.Add(d);
                sublistItem.Style = st;
                sublistItem.Children.Add(pwdItemImg);
                sublistItem.Children.Add(pwdItemText);
                sublistItem.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double desiredWidth = sublistItem.DesiredSize.Width;
                // sublistItem.Clip = Geometry.Parse(Window_Corner(30, 100, 30 * 0.35, 1));
                sublistItem.PreviewMouseLeftButtonDown += PwdBarClick;
                sublistPanel.Children.Add(sublistItem);
            }

            // sublist.Children.Add(sublistPanel);
            scrollViewer.Content = sublistPanel;  // Instead of adding directly to Grid
            sublist.Children.Add(scrollViewer);  // Add ScrollViewer to Grid

            sublist.SizeChanged += (s, f) => {
                if (sublist.ActualWidth > 0 && sublist.ActualHeight > 0) {
                    sublist.Clip = Geometry.Parse(Window_Corner(sublist.ActualHeight, sublist.ActualWidth, squircle_radius - 0, 0.5));
                    sublist.Margin = new Thickness(point.X, point.Y - sublist.ActualHeight + 1, 0, 0);
                    System.Windows.Shapes.Path backgroundPath = new System.Windows.Shapes.Path {
                        Stroke = Brushes.Black,
                        StrokeThickness = 1,
                        // Fill = System.Windows.Media.Brushes.White, // 添加背景色
                        Data = Geometry.Parse(Window_Corner(sublist.ActualHeight, sublist.ActualWidth, squircle_radius - 0, 1))
                    };
                    for (int j = 0; j < sublistPanel.Children.Count; j++ ) {
                        if (sublistPanel.Children[j] is StackPanel p) {
                            p.Clip = Geometry.Parse(Window_Corner(itemHeight, sublist.ActualWidth - 4, squircle_radius - 2, 1));
                        }
                    }
                    sublist.Children.Add(backgroundPath);
                }
            };          

            WorkSpace.Children.Add(sublist);
            isSublistExist = sublist;
            //sublist.Focus();
        }

        private string pwd = @"C:\Users\tsunami";
        private string once = @"";
        private void Item_MouseDoubleClickForListView(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount != 2) {
                return;
            }
            System.Windows.Controls.ListViewItem temp = sender as System.Windows.Controls.ListViewItem;
            MyStruct myStruct = temp.DataContext as MyStruct; ;
            string str;
            string strType;
            if (myStruct != null) {
                //MyStruct myStruct = (MyStruct)li.Content;
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
                int index = sidePath.IndexOf(@str);
                if (index != -1) {
                    RoutedEventArgs o = new RoutedEventArgs();
                    DiskHandler(SideBar.Children[index], o);
                } else {
                    int cIndex = (int)SideBar.Tag;
                    if (cIndex != -1) {
                        Grid c = SideBar.Children[cIndex] as Grid;
                        c.ClearValue(BackgroundProperty);
                        SideBar.Tag = -1;
                    }
                    Change_ItemSource(@str);
                }
            } else {
                Process.Start(@str);
            }
        }

        private Grid recordGrid = null;
        private void Item_MouseClickForGrid(object sender, MouseButtonEventArgs e) {
            // e.LeftButton == MouseButtonState.Pressed            
            Grid temp = sender as Grid;            

            if (e.ClickCount == 1) {
                // temp.SetCurrentValue(BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")));
                recordGrid?.ClearValue(BackgroundProperty);

                //var radialBrush = new RadialGradientBrush {
                //    // 渐变中心位置（控件中心）
                //    GradientOrigin = new System.Windows.Point(0.5, 0.5),
                //    Center = new System.Windows.Point(0.5, 0.5),

                //    // 渐变半径（控制渐变范围）
                //    RadiusX = 90,
                //    RadiusY = 90,

                //    // 添加渐变停止点
                //    GradientStops = new GradientStopCollection {
                //        // 中心位置为目标颜色
                //        new GradientStop(
                //            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2962D9"),
                //            0
                //        ),        
                //        // 边缘位置为黑色
                //        new GradientStop(Colors.Black, 1)
                //    }
                //};
                //temp.Background = radialBrush;

                temp.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2962D9"));
                //System.Windows.Shapes.Path stroke_clip = new System.Windows.Shapes.Path {
                //    Stroke = System.Windows.Media.Brushes.Black,
                //    StrokeThickness = 1.5,
                //    Data = Geometry.Parse(Window_Corner(145, 110, squircle_radius, 0.5)),
                //};
                //temp.Children.Add(stroke_clip);
                //temp.Clip = Geometry.Parse(Window_Corner(35, 325, 10, 0.5));
                //temp.Effect = new DropShadowEffect {
                //    Direction = 315,
                //    ShadowDepth = 0,
                //    Color = System.Windows.Media.Color.FromArgb(128, 187, 187, 187), // 透明度50%的浅灰色
                //    Opacity = 0.5,
                //    BlurRadius = 1
                //};
                recordGrid = temp;
            }

            if (e.ClickCount != 2) {
                return;
            }

            string str;
            string strType;
            if (temp.DataContext is MyStruct myStruct) {
                //MyStruct myStruct = (MyStruct)li.Content;
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
                int index = sidePath.IndexOf(@str);
                if (index != -1) {
                    RoutedEventArgs o = new RoutedEventArgs();
                    DiskHandler(SideBar.Children[index], o);
                } else {
                    int cIndex = (int)SideBar.Tag;
                    if (cIndex != -1) {
                        Grid c = SideBar.Children[cIndex] as Grid;
                        c.ClearValue(BackgroundProperty);
                        SideBar.Tag = -1;
                    }
                    Change_ItemSource(@str);
                }
            } else {
                Process.Start(@str);
            }
        }

        //private IEnumerable<ListViewItem> GetRealizedContainers(ListView listView) {
        //    // 只枚举已生成的 ListViewItem（虚拟化下只会有屏幕里的那几十个）
        //    var stack = new Stack<DependencyObject>();
        //    stack.Push(listView);

        //    while (stack.Count > 0) {
        //        var d = stack.Pop();
        //        int count = VisualTreeHelper.GetChildrenCount(d);
        //        for (int i = 0; i < count; i++) {
        //            var child = VisualTreeHelper.GetChild(d, i);
        //            if (child is ListViewItem lvi)
        //                yield return lvi;
        //            else
        //                stack.Push(child);
        //        }
        //    }
        //}

        //private void Timer_Tick(object sender, EventArgs e) {
        //    (sender as DispatcherTimer)?.Stop();

        //    // 1) 找当前激活的 ListView
        //    ListView active = FILEINFOMATION.Visibility == Visibility.Visible ? FILEINFOMATION : GridViewContainer;

        //    // 2) 取 ScrollViewer & 视口矩形
        //    var sv = GetScrollViewer(active);
        //    if (sv == null) return;
        //    var viewport = new Rect(0, 0, sv.ViewportWidth, sv.ViewportHeight);

        //    // 3) 找到所有已实现的容器，并筛选“落在视口（可加一点上下缓冲）”的项
        //    const double pad = 60; // 预取缓冲像素（可按行高/卡片高调整）
        //    var candidates = new List<MyStruct>();

        //    foreach (var lvi in GetRealizedContainers(active)) {
        //        // 把容器位置转换到 ScrollViewer 坐标系
        //        var topLeft = lvi.TranslatePoint(new Point(0, 0), sv);
        //        var rect = new Rect(topLeft, new Size(lvi.ActualWidth, lvi.ActualHeight));

        //        if (rect.Bottom >= -pad && rect.Top <= sv.ViewportHeight + pad) {
        //            if (lvi.DataContext is MyStruct item && !item.ThumbLoaded && item.S0 == null) {
        //                candidates.Add(item);
        //            }
        //        }
        //    }

        //    if (candidates.Count == 0) return;

        //    // 4) 并行加载这些“确实可见”的项（不再用 ListInfo 下标！）
        //    var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 4 };

        //    Task.Run(() => {
        //        Parallel.ForEach(candidates, parallelOptions, item => {
        //            try {
        //                // 原来你构造路径的逻辑：pwd + item.X0
        //                string fullPath;
        //                if (pwd == @"C:\" || pwd == @"D:\" || pwd == @"E:\")
        //                    fullPath = pwd + item.X0; // 盘根目录
        //                else
        //                    fullPath = System.IO.Path.Combine(pwd, item.X0);

        //                // 用文件名构造缩略图缓存路径（你原有逻辑）
        //                string thumbPath = $@"C:\Users\tsunami\AppData\Local\WinFinder\PreviewThumbnail\{item.X0}.png";
        //                string ext = item.X2; // 你之前用 X2 作为格式（扩展名/类型）

        //                // 并发防抖：标记为“正在加载/已加载”
        //                // 简单点：乐观检查
        //                if (item.ThumbLoaded) return;
        //                item.ThumbLoaded = true;

        //                // ======= 把你原来的分支逻辑搬过来，但用 item 和 fullPath =======
        //                // 示例：文件夹
        //                if (ext == "文件夹") {
        //                    Dispatcher.InvokeAsync(() => item.S0 = bm /* 你上面准备的图标 */ );
        //                    return;
        //                }

        //                // 文本 / PDF / SVG / 压缩包 / 其它 ...（照你原来的逻辑分支）
        //                // 注意所有 set S0 的地方都改成基于 'item' 而不是 ListInfo[localIndex]
        //                // 例如 PDF:
        //                if (ext == "PDF") {
        //                    if (!File.Exists(thumbPath)) {
        //                        var p = new Process();
        //                        p.StartInfo.CreateNoWindow = true;
        //                        p.StartInfo.UseShellExecute = false;
        //                        p.StartInfo.FileName = "pwsh";
        //                        p.StartInfo.Arguments = $"-Command magick -density 50 '\"{fullPath}\"[0]' -background white -alpha remove '\"{thumbPath}\"'";
        //                        p.Start(); p.WaitForExit(); p.Close();
        //                    }
        //                    using (var gs = new FileStream(thumbPath, FileMode.Open)) {
        //                        var gm = new BitmapImage();
        //                        gm.BeginInit();
        //                        gm.DecodePixelWidth = 210;
        //                        gm.StreamSource = gs;
        //                        gm.CacheOption = BitmapCacheOption.OnLoad;
        //                        gm.EndInit();
        //                        gm.Freeze();
        //                        Dispatcher.InvokeAsync(() => item.S0 = gm);
        //                    }
        //                    return;
        //                }

        //                // ……(省略：把你原先 for/if 内所有分支都替换成以 item 为单位的代码)……

        //            } catch {
        //                // 出错可以把 item.ThumbLoaded 复位，视需要
        //                item.ThumbLoaded = false;
        //            }
        //        });
        //    });
        //}


        private int itemsNum = 0;
        private void Timer_Tick(object sender, EventArgs e) {            
            //Prevent timer from looping
            (sender as DispatcherTimer).Stop();

            ItemCollection ViewSeq;
            int rowIndex = (int)Math.Floor(offsetScroll / 30);
            if (GridViewContainer.IsVisible == true) {
                int rowIndexGrid = (int)Math.Floor(offsetScroll / gridHeight);
                if (isZoom == 0) {
                    rowIndex = rowIndexGrid * 8;
                } else {
                    rowIndex = rowIndexGrid * 17;
                }
                ViewSeq = GridViewContainer.Items;
            } else {
                ViewSeq = FILEINFOMATION.Items;
            }          
            
            string preStr = @pwd;
            if (@pwd == @"C:\" || @pwd == @"D:\" || @pwd == @"E:\") {
                preStr = pwd.Replace(@"\", @"");
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

            FileStream ks = new FileStream(@"E:\Repo\wpf\WpfApp2\ico\open-7z-file.png", FileMode.Open);
            BitmapImage km = new BitmapImage();
            km.BeginInit();
            km.DecodePixelWidth = 210;
            km.StreamSource = ks;
            km.CacheOption = BitmapCacheOption.OnLoad;
            km.EndInit();
            ks.Dispose();
            km.Freeze();

            FileStream ts = new FileStream(@"E:\Repo\wpf\WpfApp2\ico\binary.png", FileMode.Open);
            BitmapImage tm = new BitmapImage();
            tm.BeginInit();
            tm.DecodePixelWidth = 210;
            tm.StreamSource = ts;
            tm.CacheOption = BitmapCacheOption.OnLoad;
            tm.EndInit();
            ts.Dispose();
            tm.Freeze();

            FileStream xs = new FileStream(@"E:\Repo\wpf\WpfApp2\ico\ini.png", FileMode.Open);
            BitmapImage xm = new BitmapImage();
            xm.BeginInit();
            xm.DecodePixelWidth = 100;
            xm.StreamSource = xs;
            xm.CacheOption = BitmapCacheOption.OnLoad;
            xm.EndInit();
            xs.Dispose();
            xm.Freeze();

            Task.Run(() => {
                string pathvar = Environment.GetEnvironmentVariable("PATH");
                Environment.SetEnvironmentVariable("PATH", pathvar + @";");
                MemoryStream ms = new MemoryStream();

                ParallelOptions parallelOptions = new ParallelOptions();
                parallelOptions.MaxDegreeOfParallelism = 4;
                Parallel.For(0, 136, parallelOptions, i => {
                    //Thread.Sleep(2000);
                    try {
                        int localIndex = i + rowIndex;
                        if (localIndex >= 0 && localIndex < itemsNum) {
                            if ((ViewSeq[localIndex] as MyStruct).ThumbLoaded == false) {
                                (ViewSeq[localIndex] as MyStruct).ThumbLoaded = true;
                                string fko = @preStr + @"\" + (ViewSeq[localIndex] as MyStruct).X0;                               
                                string fmo = $"\"{fko}\"";
                                if ((ViewSeq[localIndex] as MyStruct).X2 == "文件夹") {
                                    Dispatcher.InvokeAsync(new Action(delegate {
                                        (ViewSeq[localIndex] as MyStruct).S0 = bm;
                                    }));
                                } else {                                  
                                    string tko = $@"C:\Users\tsunami\AppData\Local\WinFinder\PreviewThumbnail\{(ViewSeq[localIndex] as MyStruct).X0}.png";
                                    string tmo = $"\"{tko}\"";
                                    string fname = (ViewSeq[localIndex] as MyStruct).X0;
                                    string format = (ViewSeq[localIndex] as MyStruct).X2;
                                    string[] textFormatSets = {"BAT", "H", "LICENSE", "README", "TXT", "XML", "XAML", "LOG", "CUB", "CSS", "CSV", "IPYNB", "GBS", "PDB", "PY", "CPP", "HTML", "C", "AHK", "MD", "YAML", "JSON", "JS", "TS"};
                                    if ((fname[0] == '.' && fname.Substring(1).ToUpper() == format) || textFormatSets.Contains(format) == true) {
                                        if (!File.Exists(tko)) {
                                            try {
                                                ProcessStartInfo sa = new ProcessStartInfo {
                                                    FileName = "pwsh",
                                                    //Arguments = $"-Command get-content -path '\"{fko}\"' -TotalCount 60 | magick -size 450x600 -background white -fill black -font \"\"\"Microsoft-YaHei-&-Microsoft-YaHei-UI\"\"\" -pointsize 18 label:@- -bordercolor white -border 20 PNG:- | magick - -alpha set -gravity NorthEast -define compose:outside-overlay=false '\"E:\\Repo\\wpf\\WpfApp2\\ico\\mask2.png\"' -compose DstIn -composite '\"E:\\Repo\\wpf\\WpfApp2\\ico\\G3.png\"' -compose Multiply -composite '\"{tko}\"'",
                                                    Arguments = $"-Command get-content -path '\"{fko}\"' -TotalCount 60 | magick -size 450x600 -background white -fill black -font \"\"\"Microsoft-YaHei-&-Microsoft-YaHei-UI\"\"\" -pointsize 18 label:@- -bordercolor white -border 20 '\"{tko}\"'",
                                                    //Arguments = $"-Command get-content -path '\"{fko}\"' -TotalCount 60 | magick -size 450x600 -background white -fill black -font \"\"\"NEOXIHEI\"\"\" -pointsize 18 label:@- -bordercolor white -border 20 '\"{tko}\"'",
                                                    //Arguments = $"-Command get-content -path '\"{fko}\"' -TotalCount 60 | magick -size 450x600 -background white -fill black -font \"\"\"NEOXIHEI\"\"\" -pointsize 18 label:@- -bordercolor white -border 20 PNG:- | magick - \"\"\"(\"\"\" +clone -background black -shadow 40x10+0+0 \"\"\")\"\"\" +swap -background none -layers merge +repage '\"{tko}\"'",
                                                    CreateNoWindow = true,
                                                    UseShellExecute = false,
                                                };
                                                Process oc = new Process {
                                                    StartInfo = sa,                                                    
                                                };
                                                oc.Start();                                                
                                                oc.WaitForExit();
                                                oc.Close();
                                            } catch (InvalidOperationException ew) {
                                                Trace.WriteLine($"{fko} {ew.Source} {ew.StackTrace} {ew.InnerException} {ew.Message}");
                                            }                                
                                        }
                                        try {
                                            using (FileStream gs = new FileStream(tko, FileMode.Open)) {
                                                BitmapImage gm = new BitmapImage();
                                                gm.BeginInit();
                                                gm.DecodePixelWidth = 210;
                                                gm.StreamSource = gs;
                                                gm.CacheOption = BitmapCacheOption.OnLoad;
                                                gm.EndInit();
                                                gs.Dispose();
                                                gm.Freeze();
                                                Dispatcher.InvokeAsync(new Action(delegate {
                                                    (ViewSeq[localIndex] as MyStruct).S0 = gm;
                                                }));
                                            }
                                        } catch (IOException ex) {
                                            Trace.WriteLine($"{fko} {ex.Message}");
                                        }
                                    } else {
                                        if (format == "PDF") {
                                            if (!File.Exists(tko)) {
                                                Trace.WriteLine("This branch is executed");
                                                Process oc = new Process();
                                                oc.StartInfo.CreateNoWindow = true;
                                                oc.StartInfo.UseShellExecute = false;
                                                //oc.StartInfo.FileName = "gswin64c";
                                                //oc.StartInfo.Arguments = $@"-dSAFER -dBATCH -dNOPAUSE -dFirstPage=1 -dLastPage=1 -sDEVICE=png16m -dGraphicsAlphaBits=4 -dTextAlphaBits=4 -r300 -sOutputFile={tmo} {fmo}";
                                                oc.StartInfo.FileName = "pwsh";
                                                // \"{fko}\" 防止路径中的两个空格变为一个空格 -Command 之后不应用{}框上具体的命令                                                
                                                //oc.StartInfo.Arguments = $"-Command magick -density 80 '\"{fko}\"[0]' -background white -alpha remove PNG:- | magick - -alpha set -gravity NorthEast -define compose:outside-overlay=false '\"E:\\Repo\\wpf\\WpfApp2\\ico\\mask2.png\"' -compose DstIn -composite '\"E:\\Repo\\wpf\\WpfApp2\\ico\\G3.png\"' -compose Multiply -composite '\"{tko}\"'";
                                                oc.StartInfo.Arguments = $"-Command magick -density 50 '\"{fko}\"[0]' -background white -alpha remove '\"{tko}\"'";
                                                //oc.StartInfo.Arguments = $"-Command magick -density 50 '\"{fko}\"[0]' -background white -alpha remove PNG:- | magick - \"\"\"(\"\"\" +clone -background black -shadow 40x10+0+0 \"\"\")\"\"\" +swap -background none -layers merge +repage '\"{tko}\"'";
                                                oc.Start();
                                                oc.WaitForExit();
                                                oc.Close();
                                            }
                                            try {
                                                using (FileStream gs = new FileStream(tko, FileMode.Open)) {
                                                    BitmapImage gm = new BitmapImage();
                                                    gm.BeginInit();
                                                    gm.DecodePixelWidth = 210;
                                                    gm.StreamSource = gs;
                                                    gm.CacheOption = BitmapCacheOption.OnLoad;
                                                    gm.EndInit();
                                                    gs.Dispose();
                                                    gm.Freeze();
                                                    Dispatcher.InvokeAsync(new Action(delegate {
                                                        (ViewSeq[localIndex] as MyStruct).S0 = gm;
                                                    }));
                                                }
                                            } catch (IOException ex) {
                                                Trace.WriteLine($"{fko} {ex.Message}");
                                            }
                                        } else if (format == "SVG") {
                                            if (!File.Exists(tko)) {
                                                Process oc = new Process();
                                                oc.StartInfo.CreateNoWindow = true;
                                                oc.StartInfo.UseShellExecute = false;
                                                oc.StartInfo.FileName = "pwsh";
                                                // \"{fko}\" 防止路径中的两个空格变为一个空格 -Command 之后不应用{}框上具体的命令
                                                oc.StartInfo.Arguments = $"-Command inkscape '\"{fko}\"' --export-type=png --export-area-drawing --export-background=white --export-filename='\"{tko}\"'";
                                                oc.Start();
                                                oc.WaitForExit();
                                                oc.Close();
                                            }
                                            try {
                                                using (FileStream gs = new FileStream(tko, FileMode.Open)) {
                                                    BitmapImage gm = new BitmapImage();
                                                    gm.BeginInit();
                                                    gm.DecodePixelWidth = 210;
                                                    gm.StreamSource = gs;
                                                    gm.CacheOption = BitmapCacheOption.OnLoad;
                                                    gm.EndInit();
                                                    gs.Dispose();
                                                    gm.Freeze();
                                                    Dispatcher.InvokeAsync(new Action(delegate {
                                                        (ViewSeq[localIndex] as MyStruct).S0 = gm;
                                                    }));
                                                }
                                            } catch (IOException ex) {
                                                Trace.WriteLine($"{fko} {ex.Message}");
                                            }
                                        } else if (format == "DOCX" || format == "XLSX" || format == "PPTX") {


                                        } else if (format == "ZIP" || format == "7Z") {
                                            Dispatcher.InvokeAsync(new Action(delegate {
                                                (ViewSeq[localIndex] as MyStruct).S0 = km;
                                            }));
                                        } else if (format == "INI" || format == "TBL") {
                                            Dispatcher.InvokeAsync(new Action(delegate {
                                                (ViewSeq[localIndex] as MyStruct).S0 = xm;
                                            }));
                                        } else if (format == "DLL" || format == "BIN" || format == "DAT" || format == "BBL" || format == "PFS") {
                                            Dispatcher.InvokeAsync(new Action(delegate {
                                                (ViewSeq[localIndex] as MyStruct).S0 = tm;
                                            }));
                                        } else {
                                            try {
                                                ShellObject shellUnit = ShellObject.FromParsingName(@fko);
                                                BitmapSource imp = shellUnit.Thumbnail.LargeBitmapSource;
                                                shellUnit.Dispose();
                                                imp.Freeze();
                                                Dispatcher.InvokeAsync(new Action(delegate {
                                                    (ViewSeq[localIndex] as MyStruct).S0 = imp;
                                                }));
                                            } catch (ShellException es) {
                                                Trace.WriteLine($"{(ViewSeq[localIndex] as MyStruct).X0} {@fko} {es.TargetSite} {es.Data.Count} {es.HelpLink}");
                                            }
                                        }
                                    }   
                                }
                            }
                        }
                    } catch (AggregateException ea) {
                        Trace.WriteLine($"{ListInfo[i].X0} {ea.Message}");
                    }
                });
            });
        }

        public double offsetScroll = 0;
        private void ForScrollChanged(object sender, ScrollChangedEventArgs e) {      
            offsetScroll = e.VerticalOffset;
            if (timer.IsEnabled) {
                timer.Stop();
            }            
            timer.Start();                  
        }

        public class NaturalStringComparer : IComparer<string>, IComparer {
            // 比较两个字符串
            public int Compare(string x, string y) {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                // 使用正则表达式拆分字符串
                var regex = new Regex(@"(\d+|\D+)");

                var xParts = regex.Split(x);
                var yParts = regex.Split(y);

                int maxLength = Math.Max(xParts.Length, yParts.Length);

                for (int i = 0; i < maxLength; i++) {
                    string xPart = i < xParts.Length ? xParts[i] : string.Empty;
                    string yPart = i < yParts.Length ? yParts[i] : string.Empty;

                    // 比较数字部分
                    if (int.TryParse(xPart, out int xNum) && int.TryParse(yPart, out int yNum)) {
                        int result = xNum.CompareTo(yNum);
                        if (result != 0)
                            return result;
                    } else {
                        // 比较非数字部分
                        int result = string.Compare(xPart, yPart, StringComparison.OrdinalIgnoreCase);
                        if (result != 0)
                            return result;
                    }
                }
                return 0; // 如果完全相等，返回 0
            }

            // 显式实现 IComparer 接口的 Compare 方法
            public int Compare(object x, object y) {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                // 确保我们处理的是 string 类型
                string xStr = x?.ToString();
                string yStr = y?.ToString();

                return Compare(xStr, yStr);
            }

            // 提供给结构体比较的比较器方法
            public static IComparer CreateComparerForProperty(string propertyName, ListSortDirection sortDirection) {
                return new NaturalStringComparerForProperty(propertyName, sortDirection);
            }

            // 用于从结构体提取指定属性并排序的自定义比较器
            private class NaturalStringComparerForProperty : IComparer {
                private readonly string _propertyName;
                private readonly ListSortDirection _sortDirection;

                public NaturalStringComparerForProperty(string propertyName, ListSortDirection sortDirection) {
                    _propertyName = propertyName;
                    _sortDirection = sortDirection;
                }

                public int Compare(object x, object y) {
                    if (x == null || y == null)
                        return x == null ? (y == null ? 0 : -1) : 1;

                    // 获取属性值
                    var xPropertyValue = GetPropertyValue(x, _propertyName);
                    var yPropertyValue = GetPropertyValue(y, _propertyName);

                    int comparisonResult = new NaturalStringComparer().Compare(xPropertyValue, yPropertyValue);

                    // 根据排序方向反向比较
                    return _sortDirection == ListSortDirection.Ascending ? comparisonResult : -comparisonResult;
                }

                private string GetPropertyValue(object obj, string propertyName) {
                    // 使用反射获取属性值
                    var property = obj.GetType().GetProperty(propertyName);
                    if (property != null) {
                        var value = property.GetValue(obj);
                        return value?.ToString() ?? string.Empty; // 确保返回字符串
                    }
                    return string.Empty; // 如果属性不存在，返回空字符串
                }
            }
        }



        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) {
            if (!(sender is ListView view)) {
                return;
            }
            if (e.OriginalSource is GridViewColumnHeader header) {
                GridViewColumn clickedColumn = header.Column;
                TextBlock bt = header.Content as TextBlock;
                if (clickedColumn != null) {
                    //string bindingProperty = (clickedColumn.DisplayMemberBinding as Binding)?.Path.Path;
                    TextBlock dt;
                    if (clickedColumn.CellTemplate.LoadContent().ToString() == "System.Windows.Controls.StackPanel") {
                        StackPanel sk = clickedColumn.CellTemplate.LoadContent() as StackPanel;
                        dt = sk.Children[1] as TextBlock;
                    } else if (clickedColumn.CellTemplate.LoadContent().ToString() == "System.Windows.Controls.Grid") {
                        Grid sk = clickedColumn.CellTemplate.LoadContent() as Grid;
                        dt = sk.Children[0] as TextBlock;
                    } else {
                        dt = clickedColumn.CellTemplate.LoadContent() as TextBlock;
                    }
                    System.Windows.Data.Binding myBinding;
                    if (bt.Text == "大小") {
                        myBinding = BindingOperations.GetBinding(dt, TagProperty);
                    } else {
                        myBinding = BindingOperations.GetBinding(dt, TextBlock.TextProperty);
                    }
                    string bindingProperty = myBinding?.Path.Path;
                    if (bindingProperty == null) {
                        bindingProperty = header.Tag.ToString();
                        if (string.IsNullOrEmpty(bindingProperty)) {
                            return;
                        }
                    }
                    
                    SortDescriptionCollection sdc = view.Items.SortDescriptions;
                    
                    ListSortDirection sortDirection = ListSortDirection.Descending;
                    foreach (var sd in sdc) {
                        if (sd.PropertyName.Equals(bindingProperty)) {
                            sortDirection = (ListSortDirection)(((int)sd.Direction) ^ 1);
                            sdc.Remove(sd);
                            break;
                        }
                    }                    

                    sdc.Insert(0, new SortDescription(bindingProperty, sortDirection));

                    // 获取 ICollectionView
                    if (CollectionViewSource.GetDefaultView(view.ItemsSource) is ListCollectionView collectionView) {
                        collectionView.SortDescriptions.Clear();

                        // Create a custom comparer to sort based on the bindingProperty
                        collectionView.CustomSort = NaturalStringComparer.CreateComparerForProperty(bindingProperty, sortDirection);
                        

                        if (timer.IsEnabled) {
                            timer.Stop();
                        }
                        timer.Start();
                        // Add SortDescription to the view to ensure proper sorting by bindingProperty
                        // collectionView.SortDescriptions.Add(new SortDescription(bindingProperty, ListSortDirection.Ascending));
                    }
                }
            }
        }

        private void ApplyNaturalSort(string property, ListSortDirection direction) {
            // 如果两个视图共用同一个 ItemsSource（ListInfo），
            // 直接对默认视图设排序即可，两个 ListView 都会看到同样的排序。
            if (CollectionViewSource.GetDefaultView(ListInfo) is ListCollectionView lcv) {
                lcv.CustomSort = NaturalStringComparer.CreateComparerForProperty(property, direction);
                // lcv.SortDescriptions.Clear(); // 保留一条 SortDescription 便于你后续点列头时反转
                // lcv.SortDescriptions.Add(new SortDescription(property, direction));
                // lcv.Refresh();
            }
        }

        private void ViewListView_Click(object sender, RoutedEventArgs e) {
            GridViewContainer.Visibility = Visibility.Collapsed;
            FILEINFOMATION.Visibility = Visibility.Visible;
            headerline.Visibility = Visibility.Visible;

            // 确保选中项可见
            EnsureSelectedItemsVisible(FILEINFOMATION);
        }

        private void ViewGridView_Click(object sender, RoutedEventArgs e) {
            FILEINFOMATION.Visibility = Visibility.Collapsed;
            GridViewContainer.Visibility = Visibility.Visible;
            headerline.Visibility = Visibility.Collapsed;

            // 确保选中项可见
            EnsureSelectedItemsVisible(GridViewContainer);
        }

        // 添加代码
        private void EnsureSelectedItemsVisible(System.Windows.Controls.ListView listView) {
            if (listView.Items.Count == 0) return;

            // 查找第一个选中项
            var selectedItem = ListInfo.FirstOrDefault(item => item.IsSelected);

            if (selectedItem != null) {
                try {
                    listView.ScrollIntoView(selectedItem);

                    // 对于虚拟化面板，可能需要额外处理
                    if (listView.ItemContainerGenerator.ContainerFromItem(selectedItem) is System.Windows.Controls.ListViewItem item) {
                        item.BringIntoView();
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"滚动到选中项错误: {ex.Message}");
                }
            } else {
                // 没有选中项时滚动到顶部
                ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(listView);
                scrollViewer?.ScrollToTop();
            }
        }

        // 辅助方法：查找可视化子元素
        private static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T result) {
                    return result;
                }

                T childItem = FindVisualChild<T>(child);
                if (childItem != null) return childItem;
            }
            return null;
        }


        // 原有代码
        private static T GetVisualChild<T>(DependencyObject parent) where T : Visual {
            T child = default(T);

            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++) {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null) {
                    child = GetVisualChild<T>(v);
                }
                if (child != null) {
                    break;
                }
            }
            return child;
        }
    }
    public class MyStruct : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isSelected;
        public bool IsSelected {
            get { return _isSelected; }
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
                }
            }
        }

        private bool _thumbLoaded;
        public bool ThumbLoaded {
            get => _thumbLoaded;
            set { _thumbLoaded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbLoaded))); }
        }

        private string _x0; private string _x1; private string _x2; private string[] _x3; private long _x4;
        public string X0 {
            get {
                return _x0;
            }
            set {
                _x0 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X0"));
            }
        }
        public string X1 {
            get {
                return _x1;
            }
            set {
                _x1 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X1"));
            }
        }
        public string X2 {
            get {
                return _x2;
            }
            set {
                _x2 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X2"));
            }
        }
        public string[] X3 {
            get {
                return _x3;
            }
            set {
                _x3 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X3"));
            }
        }
        public long X4 {
            get {
                return _x4;
            }
            set {
                _x4 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X4"));
            }
        }

        public BitmapSource s0;
        public BitmapSource S0 {
            get {
                return s0;
            }
            set {
                s0 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("S0"));
            }
        }
    }
}
