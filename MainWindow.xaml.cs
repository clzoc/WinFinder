using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections;
using SharpVectors.Converters;
using static System.Net.WebRequestMethods;
using SharpVectors.Dom;
using System.Windows.Shell;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using System.Drawing.Configuration;
using System.Windows.Interop;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.WindowsAPICodePack.Shell;
using System.Runtime.InteropServices.ComTypes;
using System.Reflection;
using System.Runtime.InteropServices.CustomMarshalers;
using SharpVectors.Dom.Events;
using System.Windows.Threading;
using System.Timers;


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
            FILEINFOMATION.ItemsSource = ListInfo;
            GridViewContainer.ItemsSource = ListInfo;
        }

        public DispatcherTimer timer = new DispatcherTimer {
            Interval = new TimeSpan(0, 0, 0, 0, 50)
        };

        public event PropertyChangedEventHandler PropertyChanged;

        private void MainWindow_Resize(object sender, EventArgs e) {
            Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
            ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, fileHeight * 0.35, 1);
        }

        private readonly string[] icon = { "/icon/Maximize_Button_Hover_M.svg", "/icon/Maximize_Button_Hover_Zoom_M.svg" };

        private static readonly double squircle_radius = 20;

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

        private string gridSpaceInfo = "";
        public string GridSpaceInfo {
            get {
                return gridSpaceInfo;
            }
            set {
                gridSpaceInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("GridSpaceInfo"));
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

                Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
                ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth - 1, fileHeight * 0.35, 0.5);

                //GridSpaceInfo = GridSpace();
                //StretchGridViewContainer();
            } else {
                double h = Height;
                double w = Width;
                Height = 900 + 2;
                Width = 1200 + 2;
                Top = 0.5 * (h - Height);
                Left = 0.5 * (w - Width);
                isZoom = 0;
                ZoomButton = icon[isZoom];

                Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
                ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth - 1, fileHeight * 0.35, 0.5);

                //GridSpaceInfo = GridSpace();
                //StretchGridViewContainer();
            }
            //WindowState = WindowState.Maximized;
        }

        public static readonly double fileHeight = 30;

        public ObservableCollection<MyStruct> ListInfo = new ObservableCollection<MyStruct>();        

        private List<string> sidePath = new List<string> { @"C:\Users\tsunami", @"C:\Users\tsunami\Desktop", @"C:\Users\tsunami\Downloads", @"C:\Users\tsunami\Pictures", @"C:\Users\tsunami\Videos", @"C:\Users\tsunami\Documents", };

        private void ContentView(object sender, RoutedEventArgs e) {
            GridClipInfo = Window_Corner(175, 115, squircle_radius, 0.5);

            List<string> side = new List<string> { "tsunami", "桌面", "下载", "图片", "视频", "文稿", };
            List<string> icon = new List<string> {
                "/icon/folder.badge.person.crop.svg",
                "/icon/house.fill.svg",
                "/icon/tray.and.arrow.down.fill.svg",
                "/icon/camera.fill.svg",
                "/icon/video.fill.svg",
                "/icon/doc.fill.svg",
            };
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo item in drives) {
                sidePath.Add(item.Name);
                side.Add($"{item.DriveType}({item.Name})");
                icon.Add("/icon/internaldrive.fill.svg");
            }

            Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
            ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth - 1, fileHeight * 0.35, 0.5);


            PathBack.Height = 35;
            PathBack.Width = 35;
            PathBack.HorizontalAlignment = HorizontalAlignment.Left;
            PathBack.VerticalAlignment = VerticalAlignment.Center;
            PathBack.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 1));

            PathMove.Height = 35;
            PathMove.Width = 35;
            PathMove.HorizontalAlignment = HorizontalAlignment.Left;
            PathMove.VerticalAlignment = VerticalAlignment.Center;
            PathMove.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 1));

            ViewListView.Height = 35;
            ViewListView.Width = 35;
            ViewListView.HorizontalAlignment = HorizontalAlignment.Left;
            ViewListView.VerticalAlignment = VerticalAlignment.Center;
            ViewListView.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 1));

            ViewGridView.Height = 35;
            ViewGridView.Width = 35;
            ViewGridView.HorizontalAlignment = HorizontalAlignment.Left;
            ViewGridView.VerticalAlignment = VerticalAlignment.Center;
            ViewGridView.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 1));

            double sideitemheight = 50;
            SideClipInfo = Window_Corner(sideitemheight, SideBar.ActualWidth, sideitemheight * 0.35, 1);

            SideBar.Tag = 0;
            for (int i = 0; i < side.Count; i++) {
                Grid t = new Grid {
                    Height = sideitemheight,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                Binding m = new Binding("Data") {
                    Source = SideClip
                };
                t.SetBinding(ClipProperty, m);
                t.Tag = sidePath[i];
                t.MouseLeftButtonDown += DiskHandler;

                //if (i != 0) {
                //    DataTrigger d = new DataTrigger {
                //        Binding = new Binding("IsMouseOver") { Source = t },
                //        Value = true,
                //    };
                //    d.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")) });
                //    Style st = new Style();
                //    st.Triggers.Add(d);
                //    t.Style = st;
                //} else {
                //    t.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8"));
                //}

                DataTrigger d = new DataTrigger {
                    Binding = new Binding("IsMouseOver") { Source = t },
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
                if (i == 5) {
                    s = new SvgViewbox {
                        Height = t.Height - 25,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(32.5, 0, 0, 0),
                        Source = new Uri(icon[i], UriKind.Relative),
                    };
                } else {
                    s = new SvgViewbox {
                        Width = t.Height - 25,
                        Height = Width,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(30, 0, 0, 0),
                        Source = new Uri(icon[i], UriKind.Relative),
                    };
                }

                TextBlock textBlock = new TextBlock {
                    Text = side[i],
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(70, 0, 0, 0),
                };

                _ = t.Children.Add(s);
                _ = t.Children.Add(textBlock);

                _ = SideBar.Children.Add(t);
            }

            Change_ItemSource(@"C:\Users\tsunami");
            //StretchGridViewContainer();
        }


        private void DiskHandler(object sender, RoutedEventArgs e) {
            Grid g = sender as Grid;
            string s = g.Tag as string;
            int gIndex = SideBar.Children.IndexOf(g);
            int cIndex = (int)SideBar.Tag;
            if (s != @pwd) {

                Change_ItemSource(@s);
                //StretchGridViewContainer();

                g.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8"));
                Grid c = SideBar.Children[cIndex] as Grid;
                c.ClearValue(BackgroundProperty);
                //c.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("Transparent"));
                //DataTrigger d = new DataTrigger {
                //    Binding = new Binding("IsMouseOver") { Source = c },
                //    Value = true,
                //};
                //d.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")) });
                //Style st = new Style();
                //st.Triggers.Add(d);
                //c.Style = st;

                SideBar.Tag = gIndex;
            }
            return;
        }

        private static readonly string[] suffixes = new string[] { " B", " KB", " MB", " GB", " TB", " PB" };
        public string ByteToValue(long number) {
            double last = 1;
            for (int i = 0; i < suffixes.Length; i++) {
                double current = Math.Pow(1024, i + 1);
                double temp = number / current;
                if (temp < 1) {
                    return (number / last).ToString("n2") + suffixes[i];
                }
                last = current;
            }
            return number.ToString();
        }

        private void Window_Back(object sender, RoutedEventArgs e) {
            DirectoryInfo di = new DirectoryInfo(@pwd);
            if (pwd != @"D:\" && pwd != @"C:\") {
                string next = di.Parent.FullName;
                int index = sidePath.IndexOf(@next);
                once = pwd;
                if (index != -1) {
                    RoutedEventArgs o = new RoutedEventArgs();
                    DiskHandler(SideBar.Children[index], o);
                } else {
                    Change_ItemSource(@next);
                    //StretchGridViewContainer();
                }
            } else {
                return;
            }
        }

        private void Window_Retu(object sender, RoutedEventArgs e) {
            if (once == "") {
                return;
            }
            Change_ItemSource(@once);
            //StretchGridViewContainer();
        }

        private string pwd = @"C:\Users\tsunami";
        private string once = @"";
        private void Item_MouseDoubleClickForListView(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount != 2) {
                return;
            }
            ListViewItem temp = sender as ListViewItem;
            MyStruct myStruct = temp.DataContext as MyStruct; ;
            string str;
            string strType;
            if (myStruct != null) {
                //MyStruct myStruct = (MyStruct)li.Content;
                if (pwd == @"C:\" || pwd == @"D:\") {
                    str = $@"{pwd}{myStruct.X0}";
                } else {
                    str = $@"{pwd}\{myStruct.X0}";
                }
                strType = myStruct.X2;
            } else {
                Grid s = sender as Grid;
                TextBlock t = s.Children[1] as TextBlock;
                if (pwd == @"C:\" || pwd == @"D:\") {
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
                    Change_ItemSource(@str);
                    //StretchGridViewContainer();
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
                //if (recordGrid != null) {
                //recordGrid.ClearValue(BackgroundProperty);
                //c.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("Transparent"));
                //DataTrigger d = new DataTrigger {
                //    Binding = new Binding("IsMouseOver") { Source = recordGrid },
                //    Value = true,
                //};
                //d.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")) });
                //Style st = new Style();
                //st.Triggers.Add(d);
                //recordGrid.Style = st;
                // recordGrid.ClearValue(BackgroundProperty);
                // Trace.WriteLine($"{recordGrid.Margin}");
                // Trace.WriteLine($"{recordGrid.ActualWidth}");
                // recordGrid.SetCurrentValue(BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                // recordGrid.Background = System.Windows.Media.Brushes.Transparent;                    
                // recordGrid.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8"));
                //}
                // temp.SetCurrentValue(BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8")));

                recordGrid?.ClearValue(BackgroundProperty);
                temp.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9ABAE8"));
                recordGrid = temp;
            }

            if (e.ClickCount != 2) {
                return;
            }

            MyStruct myStruct = temp.DataContext as MyStruct;
            string str;
            string strType;
            if (myStruct != null) {
                //MyStruct myStruct = (MyStruct)li.Content;
                if (pwd == @"C:\" || pwd == @"D:\") {
                    str = $@"{pwd}{myStruct.X0}";
                } else {
                    str = $@"{pwd}\{myStruct.X0}";
                }
                strType = myStruct.X2;
            } else {
                Grid s = sender as Grid;
                TextBlock t = s.Children[1] as TextBlock;
                if (pwd == @"C:\" || pwd == @"D:\") {
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
                    Change_ItemSource(@str);
                    //StretchGridViewContainer();
                }
            } else {
                Process.Start(@str);
            }
        }
        

        private void Change_ItemSource(string str) {
            pwd = str;
            DirectoryInfo di = new DirectoryInfo(@str);
            FileInfo[] fics = di.GetFiles();
            var fic = fics.ToList().Where(t => (t.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != (FileAttributes.Hidden | FileAttributes.System)).ToList();
            DirectoryInfo[] dics = di.GetDirectories();
            var dic = dics.ToList().Where(t => (t.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != (FileAttributes.Hidden | FileAttributes.System)).ToList();
            
            int nF = fic.Count; int nD = dic.Count;

            loadOrNot.Clear();   
            ListInfo.Clear();
            // MessageBox.Show($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            // Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            for (int i = 0; i < nF; i++) {
                ListInfo.Add(new MyStruct() { X0 = fic[i].Name, X1 = fic[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), X2 = fic[i].Extension.Replace(".", "").ToUpper(), X3 = ByteToValue(fic[i].Length), X4 = fic[i].Length });
                loadOrNot.Add(false);
            }
            for (int i = 0; i < nD; i++) {
                ListInfo.Add(new MyStruct() { X0 = dic[i].Name, X1 = dic[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), X2 = "文件夹", X3 = "— —", X4 = -1 });
                loadOrNot.Add(false);
            }

            string preStr = @pwd;
            if (@pwd == @"C:\" || @pwd == @"D:\") {
                preStr = pwd.Replace(@"\", @"");
            }            

            if (nF + nD > 0) {
                if (GridViewContainer.IsVisible == false) {
                    FILEINFOMATION.ScrollIntoView(FILEINFOMATION.Items[0]);
                } else {
                    ScrollViewer scrollViewer = VisualTreeHelper.GetChild(GridViewContainer, 0) as ScrollViewer;
                    scrollViewer.ScrollToTop();
                }
                //Task.Run(() => {
                //    Parallel.For(0, 3, i => {
                //        for (int j = 0 + i; j < Math.Min(nF + nD, 139); j += 3) {
                //            string file = @preStr + @"\" + ListInfo[j].X0;
                //            var shellUnit = ShellObject.FromParsingName(file);
                //            BitmapSource imp = shellUnit.Thumbnail.LargeBitmapSource;
                //            imp.Freeze();
                //            int localIndex = j;
                //            Dispatcher.InvokeAsync(new Action(delegate {
                //                ListInfo[localIndex].S0 = imp;
                //                loadOrNot[localIndex] = true;
                                
                //            }));
                //        }
                //    });
                    //for (int i = 0; i < Math.Min(nF + nD, 139); i++) {
                    //    string filepath = @preStr + @"\" + ListInfo[i].X0;
                    //    var shellobject = ShellObject.FromParsingName(filepath);
                    //    BitmapSource bmp = shellobject.Thumbnail.LargeBitmapSource;                  
                    //    bmp.Freeze();                        
                    //    Dispatcher.Invoke(new Action(delegate {
                    //        ListInfo[i].S0 = bmp;
                    //        loadOrNot[i] = true;
                    //    }));
                    //}
                //});
            }

            // Task.Run(() => {
            //Parallel.For(0, nF + nD, index => {
            //    string filepath = pwd + @"\" + ListInfo[index].X0;
            //    var shellobject = ShellObject.FromParsingName(@filepath);
            //    BitmapSource bmp = shellobject.Thumbnail.LargeBitmapSource;
            //    bmp.Freeze();
            //    Dispatcher.BeginInvoke(new Action(delegate { // Dispatcher.CheckAccess() 检查在UI线程上执行操作 
            //        BitmapFrame frame = BitmapFrame.Create(bmp);
            //        ListInfo[index].S0 = frame;
            //        // ListInfo[index].S0 = bmp;
            //    }));
            //});
            //});

            //string temp = pwd;
            //DriveInfo[] drives = DriveInfo.GetDrives();
            //for (int i = 0; i < drives.Length; i++) {
            //    if (drives[i].Name == pwd) {
            //        temp = pwd.Substring(0, pwd.Length - 1);
            //    }
            //}
            //Parallel.For(0, nF + nD, index => {
            //    string filepath = $@"{temp}\{ListInfo[index].X0}";
            //    var shellobject = ShellObject.FromParsingName(@filepath);
            //    Dispatcher.BeginInvoke(new Action(delegate {
            //        BitmapSource bmp = shellobject.Thumbnail.LargeBitmapSource;
            //        ListInfo[index].S0 = bmp;
            //        shellobject.Dispose();
            //    }));
            //});

            //Task.Run(() => {
            //    //Thread.Sleep(4000);
            //    //MessageBox.Show($"Target Route Path is {pwd}");

            //    for (int i = 0; i < nF; i++) {
            //        Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(fic[i].FullName);
            //        Bitmap bmp = icon.ToBitmap();
            //        BitmapSource smp = ToBitmapSource(bmp);
            //        Dispatcher.Invoke(new Action(delegate {
            //            BitmapFrame frame = BitmapFrame.Create(smp);
            //            ListInfo[i].S0 = frame;
            //        }));
            //    }
            //    for (int i = 0; i < nD; i++) {
            //        SHFILEINFO shinfo = new SHFILEINFO();
            //        IntPtr iconIntPrt = SHGetFileInfo(dic[i].FullName, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
            //        Icon icon = System.Drawing.Icon.FromHandle(shinfo.hIcon);
            //        Bitmap bmp = icon.ToBitmap();
            //        BitmapSource smp = ToBitmapSource(bmp);
            //        Dispatcher.Invoke(new Action(delegate {
            //            BitmapFrame frame = BitmapFrame.Create(smp);
            //            ListInfo[i + nF].S0 = frame;
            //        }));
            //    }
            //});
        }

        //private BitmapSource ToBitmapSource(Bitmap bitmap) {
        //    MemoryStream stream = new MemoryStream();
        //    bitmap.Save(stream, ImageFormat.Png);
        //    stream.Position = 0;
        //    BitmapImage bitmapImage = new BitmapImage();
        //    bitmapImage.BeginInit();
        //    bitmapImage.StreamSource = stream;
        //    bitmapImage.EndInit();
        //    return bitmapImage;
        //}

        //[StructLayout(LayoutKind.Sequential)] // Struct used by SHGetFileInfo function
        //private struct SHFILEINFO {
        //    public IntPtr hIcon;
        //    public int iIcon;
        //    public uint dwAttributes;
        //    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        //    public string szDisplayName;
        //    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        //    public string szTypeName;
        //};

        //[DllImport("shell32.dll")]
        //private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        //private const uint SHGFI_ICON = 0x100;
        //private const uint SHGFI_LARGEICON = 0x0;
        //private const uint SHGFI_SMALLICON = 0x000000001;

        public ObservableCollection<bool> loadOrNot = new ObservableCollection<bool>();
        private void Timer_Tick(object sender, EventArgs e) {
            //Prevent timer from looping
            (sender as DispatcherTimer).Stop();

            DirectoryInfo di = new DirectoryInfo(@pwd);
            FileInfo[] fics = di.GetFiles();
            var fic = fics.ToList().Where(t => (t.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != (FileAttributes.Hidden | FileAttributes.System)).ToList();
            DirectoryInfo[] dics = di.GetDirectories();
            var dic = dics.ToList().Where(t => (t.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != (FileAttributes.Hidden | FileAttributes.System)).ToList();

            int nF = fic.Count; int nD = dic.Count;

            int rowIndex = (int)Math.Floor(offsetScroll / 30);
            if (GridViewContainer.IsVisible == true) {
                int rowIndexGrid = (int)Math.Floor(offsetScroll / 175);
                if (isZoom == 0) {
                    rowIndex = rowIndexGrid * 8;
                } else {
                    rowIndex = rowIndexGrid * 17;
                }
            }

            string preStr = @pwd;
            if (@pwd == @"C:\" || @pwd == @"D:\") {
                preStr = pwd.Replace(@"\", @"");
            }
            //for (int i = Math.Max(0, rowIndex - 10); i < Math.Min(nF + nD, rowIndex + 129); i++) {
            //    string filepath = @preStr + @"\" + ListInfo[i].X0;
              
            //    int localIndex = i;
            //    if (loadOrNot[localIndex] == false) {
            //        var shellobject = ShellObject.FromParsingName(filepath);
            //        BitmapSource bmp = shellobject.Thumbnail.LargeBitmapSource;
            //        ListInfo[localIndex].S0 = bmp;
            //        loadOrNot[localIndex] = true;
            //    }
            //}
            Task.Run(() => {

                //Parallel.For(0, 3, i => {
                //    for (int j = Math.Max(0, rowIndex - 10) + i; j < Math.Min(nF + nD, rowIndex + 129); j += 3) {
                //        string file = @preStr + @"\" + ListInfo[j].X0;
                //        var shellUnit = ShellObject.FromParsingName(file);
                //        BitmapSource imp = shellUnit.Thumbnail.LargeBitmapSource;
                //        imp.Freeze();
                //        int localIndex = j;
                //        Dispatcher.InvokeAsync(new Action(delegate {
                //            if (loadOrNot[localIndex] == false) {
                //                ListInfo[localIndex].S0 = imp;
                //                loadOrNot[localIndex] = true;
                //            }
                //        }));
                //    }
                //});

                for (int i = Math.Max(0, rowIndex - 10); i < Math.Min(nF + nD, rowIndex + 129); i++) {
                    string filepath = @preStr + @"\" + ListInfo[i].X0;
                    var shellobject = ShellObject.FromParsingName(filepath);
                    BitmapSource bmp = shellobject.Thumbnail.LargeBitmapSource;
                    bmp.Freeze();
                    Dispatcher.Invoke(new Action(delegate {
                        if (loadOrNot[i] == false) {
                            ListInfo[i].S0 = bmp;
                            loadOrNot[i] = true;
                        }
                    }));
                }
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
                    } else {
                        dt = clickedColumn.CellTemplate.LoadContent() as TextBlock;
                    }
                    Binding myBinding;
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
                    ListSortDirection sortDirection = ListSortDirection.Ascending;
                    foreach (var sd in sdc) {
                        if (sd.PropertyName.Equals(bindingProperty)) {
                            sortDirection = (ListSortDirection)(((int)sd.Direction) ^ 1);
                            sdc.Remove(sd);
                            break;
                        }
                    }
                    sdc.Insert(0, new SortDescription(bindingProperty, sortDirection));
                }
            }
        }

        private void ViewListView_Click(object sender, RoutedEventArgs e) {
            GridViewContainer.Visibility = Visibility.Collapsed;
            FILEINFOMATION.Visibility = Visibility.Visible;
        }

        private void ViewGridView_Click(object sender, RoutedEventArgs e) {
            FILEINFOMATION.Visibility = Visibility.Collapsed;
            GridViewContainer.Visibility = Visibility.Visible;
            //GridSpaceInfo = GridSpace();
            //StretchGridViewContainer();
        }

        private Panel GetItemsPanel(DependencyObject itemsControl) {
            ItemsPresenter itemsPresenter = GetVisualChild<ItemsPresenter>(itemsControl);
            Panel itemsPanel = VisualTreeHelper.GetChild(itemsPresenter, 0) as Panel;
            return itemsPanel;
        }

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

        private void StretchGridViewContainer() {
            if (GridViewContainer.Visibility != Visibility.Visible) {
                return;
            }
            GridViewContainer.UpdateLayout();
            // double w = Math.Floor(RefGrid.ActualWidth) - 1;
            double w = Math.Floor(RefGrid.ActualWidth);
            int numberOfEachRow = (int)Math.Floor(w / 130);
            int space = numberOfEachRow - 1;
            int numberOfItems = GridViewContainer.Items.Count;
            double spd = (w % 130) / (2 * numberOfEachRow);

            WrapPanel wrapContainer = GetItemsPanel(GridViewContainer) as WrapPanel;

            for (int i = 0; i < numberOfItems; i++) {
                Grid indexHandside = VisualTreeHelper.GetChild(wrapContainer.Children[i], 0) as Grid;
                if (i % numberOfEachRow == 0) {
                    indexHandside.Margin = new Thickness(0, 0, 2 * spd, 0);
                    indexHandside.UpdateLayout();
                } else if (i % numberOfEachRow == space) {
                    indexHandside.Margin = new Thickness(2 * spd, 0, 0, 0);
                    indexHandside.UpdateLayout();
                } else {
                    indexHandside.Margin = new Thickness(spd, 0, spd, 0);
                    indexHandside.UpdateLayout();
                }
            }
            //TextBlock tttks = rightHandside.Children[1] as TextBlock;
            //Trace.WriteLine($"For Debug Information {tttks.Text}");

            //GridViewContainer.UpdateLayout();
            //double kzu = 0;
            //for (int i = 0; i < numberOfEachRow; i++) {
            //    Grid rightHandside = VisualTreeHelper.GetChild(wrapContainer.Children[i], 0) as Grid;
            //    kzu += rightHandside.ActualWidth;
            //}
            //kzu += spd * 2 * numberOfEachRow;
            //Trace.WriteLine($"For Debug Information {kzu}");
            //Trace.WriteLine($"For Debug Information {w}");
            //Trace.WriteLine($"For Debug Information {wrapContainer.ActualWidth}");

            //Grid ktv = VisualTreeHelper.GetChild(knv.Children[0], 0) as Grid;
            //Trace.WriteLine($"For Debug Information {GridViewContainer.ItemContainerGenerator.ContainerFromIndex(0)}");
            //MyStruct kkk = (GridViewContainer.Items)[1] as MyStruct;
            //Trace.WriteLine($"For Debug Information {kkk.X0}");
            //Trace.WriteLine($"For Debug Information {GetItemsPanel(GridViewContainer)}");
        }

    }
    public class MyStruct : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        public string x0; public string x1; public string x2; public string x3; public long x4;
        public string X0 {
            get {
                return x0;
            }
            set {
                x0 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X0"));
            }
        }
        public string X1 {
            get {
                return x1;
            }
            set {
                x1 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X1"));
            }
        }
        public string X2 {
            get {
                return x2;
            }
            set {
                x2 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X2"));
            }
        }
        public string X3 {
            get {
                return x3;
            }
            set {
                x3 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X3"));
            }
        }
        public long X4 {
            get {
                return x4;
            }
            set {
                x4 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("X4"));
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
