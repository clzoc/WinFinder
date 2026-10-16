using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace WinFinder.Thumbnailer.Worker.Common {
    public static class ExternalProcessRunner {
        public static ExternalProcessResult Run(string fileName, string arguments, int timeoutMs) {
            var result = new ExternalProcessResult {
                ExitCode = -1,
                StandardError = string.Empty,
                StandardOutput = string.Empty
            };

            var startInfo = new ProcessStartInfo {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                using (Process process = Process.Start(startInfo)) {
                    if (process == null) {
                        result.Diagnostic = "Failed to start process.";
                        return result;
                    }

                    result.Started = true;
                    var output = new StringBuilder();
                    var error = new StringBuilder();
                    using (var outputClosed = new ManualResetEvent(false))
                    using (var errorClosed = new ManualResetEvent(false)) {
                        process.OutputDataReceived += (sender, e) => {
                            if (e.Data == null) {
                                outputClosed.Set();
                            } else {
                                output.AppendLine(e.Data);
                            }
                        };
                        process.ErrorDataReceived += (sender, e) => {
                            if (e.Data == null) {
                                errorClosed.Set();
                            } else {
                                error.AppendLine(e.Data);
                            }
                        };

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        if (!process.WaitForExit(timeoutMs)) {
                            result.TimedOut = true;
                            TryKill(process);
                            return result;
                        }

                        outputClosed.WaitOne(100);
                        errorClosed.WaitOne(100);
                        result.StandardOutput = output.ToString();
                        result.StandardError = error.ToString();
                    }

                    result.ExitCode = process.ExitCode;
                }
            } catch (Exception ex) {
                result.Diagnostic = ex.Message;
            } finally {
                stopwatch.Stop();
                result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        private static void TryKill(Process process) {
            try {
                if (process != null && !process.HasExited) {
                    process.Kill();
                }
            } catch {
            }
        }
    }
}
