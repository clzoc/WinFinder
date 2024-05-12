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

namespace WinFinder {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    /// 

    public class User {
        public string X0 { get; set; }

        public int X1 { get; set; }

        public string X2 { get; set; }
    }

    struct MyStruct {
        public string X0 { get; set; }
        public string X1 { get; set; }
        public string X2 { get; set; }
        public string X3 { get; set; }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged {
        public MainWindow() {
            InitializeComponent();
        }

        private readonly string[] icon = { "/icon/Maximize_Button_Hover.svg", "/icon/Maximize_Button_Hover_Zoom.svg" };

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

        private int isZoom = 0;

        private string zoomButton = "/icon/Maximize_Button_Hover.svg";
        public string ZoomButton {
            get {
                return zoomButton;
            }
            set {
                zoomButton = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ZoomButton"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
            string left_bottom = $"L{f5},{h0} C{f4},{h0} {f3},{h0} {f2},{h1} A{radius},{radius} 0 0 1 {f1},{h2} C{f0},{h3} {f0},{h4} {f0},{h5}";

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
                Height = SystemParameters.WorkArea.Height + 1;
                Width = SystemParameters.WorkArea.Width;
                Top = 0;
                Left = 0;
                isZoom = 1;
                ZoomButton = icon[isZoom];

                Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
                ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 15, 0);
            } else {
                double h = Height;
                double w = Width;
                Height = 800 + 2;
                Width = 1200 + 2;
                Top = 0.5 * (h - Height);
                Left = 0.5 * (w - Width);
                isZoom = 0;
                ZoomButton = icon[isZoom];

                Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
                ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 15, 0);
            }
            //WindowState = WindowState.Maximized;
        }

        private void ContentView(object sender, RoutedEventArgs e) {
            Info = Window_Corner(ActualHeight, ActualWidth, squircle_radius, 1);
            ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 15, 0);

            PathBack.Height = 35;
            PathBack.Width = 35;
            PathBack.HorizontalAlignment = HorizontalAlignment.Left;
            PathBack.VerticalAlignment = VerticalAlignment.Center;
            PathBack.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 0));

            PathMove.Height = 35;
            PathMove.Width = 35;
            PathMove.HorizontalAlignment = HorizontalAlignment.Left;
            PathMove.VerticalAlignment = VerticalAlignment.Center;
            PathMove.Clip = Geometry.Parse(Window_Corner(35, 35, 10, 0));

            SideClipInfo = Window_Corner(50, SideBar.ActualWidth, 15, 5);

            string[] side = { "tsunami", "桌面", "下载", "图片", "影片", "音乐", "本地磁盘(C:)", "本地磁盘(D:)", };
            string[] icon = {
                "/icon/folder.badge.person.crop.svg",
                "/icon/house.fill.svg",
                "/icon/calendar.svg",
                "/icon/camera.fill.svg",
                "/icon/icloud.fill.svg",
                "/icon/headphones.circle.fill.svg",
                "/icon/internaldrive.fill.svg",
                "/icon/internaldrive.fill.svg",
            };

            for (int i = 0; i < 8; i++) {
                Grid t = new Grid {
                    Height = 50,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                Binding m = new Binding("Data") {
                    Source = SideClip
                };
                t.SetBinding(ClipProperty, m);

                if (i == 6) { t.MouseLeftButtonDown += DiskHandler; };
                if (i == 7) { t.MouseLeftButtonDown += DiskHandler; };

                DataTrigger d = new DataTrigger {
                    Binding = new Binding("IsMouseOver") { Source = t },
                    Value = true,
                };
                d.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ABAE8")) });
                Style st = new Style();
                st.Triggers.Add(d);
                t.Style = st;

                SvgViewbox s = new SvgViewbox {
                    Width = t.Height - 25,
                    Height = Width,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(20, 0, 0, 0),
                    Source = new Uri(icon[i], UriKind.Relative),
                };

                TextBlock textBlock = new TextBlock {
                    Text = side[i],
                    FontSize = 18,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(60, 0, 0, 0),
                };

                _ = t.Children.Add(s);
                _ = t.Children.Add(textBlock);

                _ = SideBar.Children.Add(t);
            }

            Change_ItemSource(FILEINFOMATION, @"C:\Users\tsunami");
            //LoadFileList(FileList, files, dics, FileClip);
        }

        private static readonly double fileHeight = 45;

        //private void LoadFileList(StackPanel s, FileInfo[] f, DirectoryInfo[] d, System.Windows.Shapes.Path p) {
        //    ClipInfo = Window_Corner(fileHeight, RefGrid.ActualWidth, 15, 0);
        //    string[] color = { "#FFFFFF", "#F4F6F5" };
        //    int num = f.Length + d.Length;

        //    for (int i = 0; i < num; i++) {
        //        int c = i & 1;

        //        Grid g = new Grid {
        //            Height = fileHeight,
        //            HorizontalAlignment = HorizontalAlignment.Stretch,
        //            //Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color[c])),
        //            ColumnDefinitions = {
        //                new ColumnDefinition {Width = new GridLength(50, GridUnitType.Pixel)},
        //                new ColumnDefinition {Width = new GridLength(3.5, GridUnitType.Star)},
        //                new ColumnDefinition {Width = new GridLength(2.0, GridUnitType.Star)},
        //                new ColumnDefinition {Width = new GridLength(1.5, GridUnitType.Star)},
        //                new ColumnDefinition {Width = new GridLength(1.0, GridUnitType.Star)},
        //            },
        //        };
        //        Binding v = new Binding("Data") {
        //            Source = p
        //        };

        //        g.SetBinding(ClipProperty, v);

        //        DataTrigger da = new DataTrigger {
        //            Binding = new Binding("IsMouseOver") { Source = g },
        //            Value = true,
        //        };
        //        da.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D5DB")) });
        //        Style st = new Style();
        //        st.Triggers.Add(da);
        //        st.Setters.Add(new Setter() { Property = BackgroundProperty, Value = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color[c])) });
        //        g.Style = st;

        //        TextBlock fName = new TextBlock {
        //            FontFamily = new FontFamily("LXGW WenKai Screen"),
        //            FontSize = 16,
        //            VerticalAlignment = VerticalAlignment.Center,
        //            Margin = new Thickness(10, 0, 0, 0),
        //        };

        //        TextBlock fuName = new TextBlock {
        //            FontFamily = new FontFamily("LXGW WenKai Screen"),
        //            FontSize = 16,
        //            VerticalAlignment = VerticalAlignment.Center,
        //            Margin = new Thickness(10, 0, 0, 0),
        //            Visibility = Visibility.Collapsed,
        //        };

        //        TextBlock fDate = new TextBlock {
        //            FontFamily = new FontFamily("LXGW WenKai Screen"),
        //            FontSize = 16,
        //            VerticalAlignment = VerticalAlignment.Center,
        //            Margin = new Thickness(10, 0, 0, 0),
        //        };

        //        TextBlock fType = new TextBlock {
        //            FontFamily = new FontFamily("LXGW WenKai Screen"),
        //            FontSize = 16,
        //            VerticalAlignment = VerticalAlignment.Center,
        //            HorizontalAlignment = HorizontalAlignment.Center,
        //        };

        //        TextBlock fSize = new TextBlock {
        //            FontFamily = new FontFamily("LXGW WenKai Screen"),
        //            FontSize = 16,
        //            VerticalAlignment = VerticalAlignment.Center,
        //            Margin = new Thickness(0, 0, 10, 0),
        //            HorizontalAlignment = HorizontalAlignment.Right,
        //        };

        //        SvgViewbox fileIcon = new SvgViewbox {
        //            Height = fileHeight - 15,
        //            Width = Height,
        //            VerticalAlignment = VerticalAlignment.Center,
        //            HorizontalAlignment = HorizontalAlignment.Center,
        //            Margin = new Thickness(15, 0, 0, 0)
        //        };

        //        if (i < d.Length) {
        //            g.MouseLeftButtonDown += MDCHandler;
        //            fName.Text = d[i].Name;
        //            fDate.Text = d[i].LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss");
        //            fType.Text = "文件夹";
        //            fSize.Text = "— —";
        //            fuName.Text = d[i].FullName;

        //            fileIcon.Source = new Uri("/icon/folder.svg", UriKind.Relative);
        //        } else {
        //            g.MouseLeftButtonDown += CTOHandler;
        //            fName.Text = f[i - d.Length].Name;
        //            fDate.Text = f[i - d.Length].LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss");
        //            fType.Text = $"{f[i - d.Length].Extension.Replace(".", "").ToUpper()} 文件";
        //            fSize.Text = ByteToValue(f[i - d.Length].Length);
        //            fuName.Text = f[i - d.Length].FullName;

        //            fileIcon.Source = new Uri("/icon/file.svg", UriKind.Relative);
        //        }

        //        _ = g.Children.Add(fileIcon);
        //        _ = g.Children.Add(fName);
        //        _ = g.Children.Add(fDate);
        //        _ = g.Children.Add(fSize);
        //        _ = g.Children.Add(fType);

        //        _ = g.Children.Add(fuName);

        //        fileIcon.SetValue(Grid.ColumnProperty, 0);
        //        fName.SetValue(Grid.ColumnProperty, 1);
        //        fDate.SetValue(Grid.ColumnProperty, 2);
        //        fType.SetValue(Grid.ColumnProperty, 3);
        //        fSize.SetValue(Grid.ColumnProperty, 4);

        //        _ = s.Children.Add(g);
        //    }
        //}

        //private void CTOHandler(object sender, MouseButtonEventArgs e) {
        //    Grid g = sender as Grid;
        //    TextBlock d = g.Children[5] as TextBlock;
        //    Process.Start(d.Text);
        //    GC.Collect();
        //}

        //private void MDCHandler(object sender, MouseButtonEventArgs e) {
        //    Grid g = sender as Grid;
        //    TextBlock d = g.Children[5] as TextBlock;
        //    DirectoryInfo di = new DirectoryInfo(@d.Text);
        //    upPath = di.Parent.FullName;
        //    FileInfo[] fs = di.GetFiles();
        //    DirectoryInfo[] dics = di.GetDirectories();
        //    FileList.Children.Clear();
        //    LoadFileList(FileList, fs, dics, FileClip);
        //    GC.Collect();
        //}

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
                once = pwd;
                Change_ItemSource(FILEINFOMATION, @next);
            } else {
                return;
            }
        }

        private void Window_Retu(object sender, RoutedEventArgs e) {
            if (once == "") {
                return;
            }
            Change_ItemSource(FILEINFOMATION, @once);
        }

        private void DiskHandler(object sender, RoutedEventArgs e) {
            Grid g = sender as Grid;
            TextBlock textBlock = g.Children[1] as TextBlock;
            string s = textBlock.Text.Substring(5, 2) + @"\";
            Change_ItemSource(FILEINFOMATION, @s);
        }

        private string pwd = @"C:\Users\tsunami";
        private string once = @"";
        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            ListViewItem li = sender as ListViewItem;
            MyStruct myStruct = (MyStruct)li.Content;
            string str = $@"{pwd}\{myStruct.X0}";
            string strType = myStruct.X2;
            if (strType == "文件夹") {
                Change_ItemSource(FILEINFOMATION, @str);
            } else {
                Process.Start(str);
            }

            //MessageBox.Show($"Target Route Path is {pwd}");
        }

        private void Change_ItemSource(ListView Target, string str) {
            pwd = str;
            DirectoryInfo di = new DirectoryInfo(@str);
            FileInfo[] files = di.GetFiles();
            DirectoryInfo[] dics = di.GetDirectories();
            List<MyStruct> items = new List<MyStruct>();
            foreach (FileInfo fo in files) {
                items.Add(new MyStruct() { X0 = fo.Name, X1 = fo.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss"), X2 = fo.Extension.Replace(".", "").ToUpper(), X3 = ByteToValue(fo.Length) });
            }
            foreach (DirectoryInfo ro in dics) {
                items.Add(new MyStruct() { X0 = ro.Name, X1 = ro.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss"), X2 = "文件夹", X3 = "— —" });
            }
            Target.ItemsSource = items;
        }
    }
}
