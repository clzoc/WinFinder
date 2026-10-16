using System;
using System.Threading;
using System.Threading.Tasks;

namespace WinFinder.Thumbnailer.Service {
    internal static class Program {
        private const string DefaultPipeName = "WinFinder.Thumbnailer.v1";

        private static int Main(string[] args) {
            string pipeName = GetOption(args, "--pipe", DefaultPipeName);
            string cacheRoot = GetOption(args, "--cache-root", null);
            long cacheBudgetMb = GetLongOption(args, "--cache-budget-mb", 0);
            if (!string.IsNullOrWhiteSpace(cacheRoot) || cacheBudgetMb > 0) {
                ThumbnailProtocol.ConfigureCache(cacheRoot, cacheBudgetMb > 0 ? cacheBudgetMb * 1024 * 1024 : 0);
            }

            if (HasArgument(args, "--self-test")) {
                return ThumbnailPipeSelfTest.Run(pipeName, includeThumbnail: false);
            }

            if (HasArgument(args, "--self-test-thumbnail")) {
                return ThumbnailPipeSelfTest.Run(pipeName, includeThumbnail: true);
            }

            if (HasArgument(args, "--diagnose")) {
                return ThumbnailDiagnostics.Run();
            }

            string pdfSelfTestPath = GetOption(args, "--self-test-pdf", null);
            if (!string.IsNullOrWhiteSpace(pdfSelfTestPath)) {
                return ThumbnailPipeSelfTest.RunPdf(pipeName, pdfSelfTestPath);
            }

            string svgSelfTestPath = GetOption(args, "--self-test-svg", null);
            if (!string.IsNullOrWhiteSpace(svgSelfTestPath)) {
                return ThumbnailPipeSelfTest.RunSvg(pipeName, svgSelfTestPath);
            }

            string shellSelfTestPath = GetOption(args, "--self-test-shell", null);
            if (!string.IsNullOrWhiteSpace(shellSelfTestPath)) {
                return ThumbnailPipeSelfTest.RunShell(pipeName, shellSelfTestPath);
            }

            string videoSelfTestPath = GetOption(args, "--self-test-video", null);
            if (!string.IsNullOrWhiteSpace(videoSelfTestPath)) {
                return ThumbnailPipeSelfTest.RunVideo(pipeName, videoSelfTestPath);
            }

            string imageSelfTestPath = GetOption(args, "--self-test-image", null);
            if (!string.IsNullOrWhiteSpace(imageSelfTestPath)) {
                return ThumbnailPipeSelfTest.RunImage(pipeName, imageSelfTestPath);
            }

            bool runOnce = HasArgument(args, "--once");
            using (var cancellation = new CancellationTokenSource()) {
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true;
                    cancellation.Cancel();
                };

                try {
                    var server = new ThumbnailPipeServer(pipeName);
                    Console.WriteLine($"WinFinder thumbnailer listening on pipe '{pipeName}'.");
                    Task.Run(() => ThumbnailProtocol.RunCacheMaintenance());
                    if (runOnce) {
                        server.RunOnce(cancellation.Token);
                    } else {
                        server.Run(cancellation.Token);
                    }

                    return 0;
                } catch (OperationCanceledException) {
                    return 0;
                } catch (Exception ex) {
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }

        private static bool HasArgument(string[] args, string name) {
            if (args == null) {
                return false;
            }

            foreach (string arg in args) {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        private static string GetOption(string[] args, string name, string fallback) {
            if (args == null) {
                return fallback;
            }

            for (int i = 0; i < args.Length - 1; i++) {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) {
                    return string.IsNullOrWhiteSpace(args[i + 1]) ? fallback : args[i + 1];
                }
            }

            return fallback;
        }

        private static long GetLongOption(string[] args, string name, long fallback) {
            string value = GetOption(args, name, null);
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            return long.TryParse(value, out long parsed) && parsed > 0 ? parsed : fallback;
        }
    }
}
