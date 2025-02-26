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
using System.Windows.Forms;


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

                Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
                ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 11, 0.5);
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
                ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 11, 0.5);
            }
        }

        

        public ObservableCollection<MyStruct> ListInfo = new ObservableCollection<MyStruct>();        

        private List<string> sidePath = new List<string> { @"C:\Users\tsunami", @"C:\Users\tsunami\Desktop", @"C:\Users\tsunami\Downloads", @"C:\Users\tsunami\Music", @"C:\Users\tsunami\Pictures", @"C:\Users\tsunami\Videos", @"C:\Users\tsunami\Documents", };

        private double fileHeight = 30;
        private double gridHeight = 150;
        private void ContentView(object sender, RoutedEventArgs e) {
            GridClipInfo = Window_Corner(gridHeight, 115, squircle_radius, 0.5);

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
            ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 11, 0.5);

            //currentFolderRegion.Clip = Geometry.Parse(Window_Corner(35, 325, 10, 0.5));

            PathBack.Height = 34;
            PathBack.Width = 34;
            PathBack.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            PathBack.VerticalAlignment = VerticalAlignment.Center;
            PathBack.Clip = Geometry.Parse(Window_Corner(34, 34, 10, 0.5));

            PathMove.Height = 34;
            PathMove.Width = 34;
            PathMove.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            PathMove.VerticalAlignment = VerticalAlignment.Center;
            PathMove.Clip = Geometry.Parse(Window_Corner(34, 34, 10, 0.5));

            ViewListView.Height = 34;
            ViewListView.Width = 34;
            ViewListView.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ViewListView.VerticalAlignment = VerticalAlignment.Center;
            ViewListView.Clip = Geometry.Parse(Window_Corner(34, 34, 10, 0.5));

            ViewGridView.Height = 34;
            ViewGridView.Width = 34;
            ViewGridView.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ViewGridView.VerticalAlignment = VerticalAlignment.Center;
            ViewGridView.Clip = Geometry.Parse(Window_Corner(34, 34, 10, 0.5));

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
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 100, 0),
                    Source = new Uri(icon[i], UriKind.Relative),
                };

                TextBlock textBlock = new TextBlock {
                    Text = side[i],
                    FontSize = 21,
                    FontWeight = FontWeights.Regular,
                    FontFamily = new System.Windows.Media.FontFamily("霞鹜新晰黑 屏幕阅读版"),
                    VerticalAlignment = VerticalAlignment.Center,
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
            List<string> pwdPa = new List<string> { };
            List<string> pwdFu = new List<string> { };
            string temp = "";
            for (int i = 0; i < str.Length; i++) {               
                if (str[i] != '\\') {
                    temp +=  str[i].ToString();
                } else {                    
                    if (temp == "C:" || temp == "D:") {
                        temp += @"\";
                        DriveInfo dr = new DriveInfo(@temp);
                        ExtraInfo.Text = $"共 {ByteToValue(dr.TotalSize)} 可用 {ByteToValue(dr.TotalFreeSpace)} ";                        
                        pwdFu.Add(temp);
                    } else {
                        pwdFu.Add(str.Substring(0, i));
                    }
                    pwdPa.Add(temp);
                    temp = "";                  
                }
            }
            if (temp != "") {
                if (temp == "C:" || temp == "D:") {
                    temp += @"\";
                }
                pwdPa.Add(temp);
                temp = "";
                pwdFu.Add(str.Substring(0));
            }

            pwdInfo.Children.Clear();
            for (int i = 0; i < pwdPa.Count; i++) {
                var shellUnit = ShellObject.FromParsingName(pwdFu[i]);
                BitmapSource imp = shellUnit.Thumbnail.LargeBitmapSource;
                shellUnit.Dispose();
                System.Windows.Controls.Image pwdItemImg = new System.Windows.Controls.Image {
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 25,
                    Width = 25,
                    Margin = new Thickness(8, 0, 5, 0),
                    Source = imp
                };

                TextBlock pwdItemText = new TextBlock {
                    Text = pwdPa[i],
                    FontSize = 15,
                    FontWeight = FontWeights.Regular,
                    FontFamily = new System.Windows.Media.FontFamily("霞鹜新晰黑 屏幕阅读版"),
                    VerticalAlignment = VerticalAlignment.Center,
                    // Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("Transparent")),
                    Margin = new Thickness(0, 0, 8, 0),
                };

                StackPanel t = new StackPanel {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Height = 34,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0),
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
                t.Clip = Geometry.Parse(Window_Corner(34, t.DesiredSize.Width - 4, 12, 0.5));

                if (i != 0) {
                    SvgViewbox s = new SvgViewbox {
                        MaxWidth = 10,
                        MaxHeight = 10,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 0),
                        Source = new Uri("icon/right.svg", UriKind.Relative),
                    };
                    pwdInfo.Children.Add(s);
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

            ExtraInfo.Text += $"共 {nF + nD} 项";

            IsLoadedFlag.Clear();
            ListInfo.Clear();

            for (int i = 0; i < nF; i++) {
                ListInfo.Add(new MyStruct() { X0 = fic[i].Name, X1 = fic[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), X2 = fic[i].Extension.Replace(".", "").ToUpper(), X3 = ByteToValue(fic[i].Length), X4 = fic[i].Length });
                IsLoadedFlag.Add(false);
            }
            for (int i = 0; i < nD; i++) {
                ListInfo.Add(new MyStruct() { X0 = dic[i].Name, X1 = dic[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), X2 = "文件夹", X3 = "— —", X4 = -1 });
                IsLoadedFlag.Add(false);
            }

            string preStr = @pwd;
            if (@pwd == @"C:\" || @pwd == @"D:\") {
                preStr = pwd.Replace(@"\", @"");
            }

            int slashIndex = (@preStr).LastIndexOf(@"\") + 1;
            CurrentFolder = (@pwd).Substring(slashIndex);

            if (nF + nD > 0) {
                if (GridViewContainer.IsVisible == false) {
                    FILEINFOMATION.ScrollIntoView(FILEINFOMATION.Items[0]);
                } else {
                    ScrollViewer scrollViewer = VisualTreeHelper.GetChild(GridViewContainer, 0) as ScrollViewer;
                    scrollViewer.ScrollToTop();
                }
                Task.Run(() => {
                    Parallel.For(0, 3, i => {
                        for (int j = 0 + i; j < Math.Min(nF + nD, 150); j += 3) {
                            string file = @preStr + @"\" + ListInfo[j].X0;
                            var shellUnit = ShellObject.FromParsingName(file);
                            BitmapSource imp = shellUnit.Thumbnail.LargeBitmapSource;
                            shellUnit.Dispose();
                            imp.Freeze();
                            int localIndex = j;
                            Dispatcher.InvokeAsync(new Action(delegate {
                                ListInfo[localIndex].S0 = imp;
                                IsLoadedFlag[localIndex] = true;
                            }));
                        }
                    });
                    //for (int i = 0; i < Math.Min(nF + nD, 139); i++) {
                    //    string filepath = @preStr + @"\" + ListInfo[i].X0;
                    //    var shellobject = ShellObject.FromParsingName(filepath);
                    //    BitmapSource bmp = shellobject.Thumbnail.LargeBitmapSource;
                    //    bmp.Freeze();
                    //    Dispatcher.Invoke(new Action(delegate {
                    //        ListInfo[i].S0 = bmp;
                    //        IsLoadedFlag[i] = true;
                    //    }));
                    //}
                });
            }
        }

        private void PwdBarClick(object sender, MouseButtonEventArgs e) {
            StackPanel s = sender as StackPanel;
            string fu = s.Tag as string;
            if (@pwd == fu) {
                return;
            }
            Change_ItemSource(@fu);
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
                temp.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2962D9"));
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

        public ObservableCollection<bool> IsLoadedFlag = new ObservableCollection<bool>();
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
                int rowIndexGrid = (int)Math.Floor(offsetScroll / gridHeight);
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

            Task.Run(() => {

                Parallel.For(0, 3, i => {
                    for (int j = Math.Max(0, rowIndex - 10) + i; j < Math.Min(nF + nD, rowIndex + 140); j += 3) {
                        string file = @preStr + @"\" + ListInfo[j].X0;
                        var shellUnit = ShellObject.FromParsingName(file);
                        BitmapSource imp = shellUnit.Thumbnail.LargeBitmapSource;
                        shellUnit.Dispose();
                        imp.Freeze();
                        int localIndex = j;
                        Dispatcher.InvokeAsync(new Action(delegate {
                            if (IsLoadedFlag[localIndex] == false) {
                                ListInfo[localIndex].S0 = imp;
                                IsLoadedFlag[localIndex] = true;
                            }
                        }));
                    }
                });

                //for (int i = Math.Max(0, rowIndex - 10); i < Math.Min(nF + nD, rowIndex + 129); i++) {
                //    string filepath = @preStr + @"\" + ListInfo[i].X0;
                //    var shellobject = ShellObject.FromParsingName(filepath);
                //    BitmapSource bmp = shellobject.Thumbnail.LargeBitmapSource;
                //    bmp.Freeze();
                //    Dispatcher.Invoke(new Action(delegate {
                //        if (IsLoadedFlag[i] == false) {
                //            ListInfo[i].S0 = bmp;
                //            IsLoadedFlag[i] = true;
                //        }
                //    }));
                //}
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
            if (!(sender is System.Windows.Controls.ListView view)) {
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
