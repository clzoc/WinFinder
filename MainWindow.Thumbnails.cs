using Microsoft.WindowsAPICodePack.Shell;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WinFinder {
    public partial class MainWindow {
        private int itemsNum = 0;
        public double offsetScroll = 0;

        private void Timer_Tick(object sender, EventArgs e) {
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
                                    string[] textFormatSets = { "BAT", "H", "LICENSE", "README", "TXT", "XML", "XAML", "LOG", "CUB", "CSS", "CSV", "IPYNB", "GBS", "PDB", "PY", "CPP", "HTML", "C", "AHK", "MD", "YAML", "JSON", "JS", "TS" };
                                    if ((fname[0] == '.' && fname.Substring(1).ToUpper() == format) || textFormatSets.Contains(format) == true) {
                                        if (!File.Exists(tko)) {
                                            try {
                                                ProcessStartInfo sa = new ProcessStartInfo {
                                                    FileName = "pwsh",
                                                    Arguments = $"-Command get-content -path '\"{fko}\"' -TotalCount 60 | magick -size 450x600 -background white -fill black -font \"\"\"Microsoft-YaHei-&-Microsoft-YaHei-UI\"\"\" -pointsize 18 label:@- -bordercolor white -border 20 '\"{tko}\"'",
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
                                                oc.StartInfo.FileName = "pwsh";
                                                oc.StartInfo.Arguments = $"-Command magick -density 50 '\"{fko}\"[0]' -background white -alpha remove '\"{tko}\"'";
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

        private void ForScrollChanged(object sender, ScrollChangedEventArgs e) {
            offsetScroll = e.VerticalOffset;
            if (timer.IsEnabled) {
                timer.Stop();
            }
            timer.Start();
        }
    }
}
