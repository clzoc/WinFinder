using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class WorkerProcessClient<TResponse> where TResponse : class {
        private readonly string workerPath;
        private readonly Func<string, TResponse> failedResponseFactory;
        private readonly string workerName;

        public WorkerProcessClient(string workerPath, string workerName, Func<string, TResponse> failedResponseFactory) {
            this.workerPath = workerPath ?? string.Empty;
            this.workerName = string.IsNullOrWhiteSpace(workerName) ? "Worker" : workerName;
            this.failedResponseFactory = failedResponseFactory ?? throw new ArgumentNullException(nameof(failedResponseFactory));
        }

        public bool Exists => File.Exists(workerPath);

        public TResponse RunCapabilities(int timeoutMs) {
            return Run("--capabilities", timeoutMs);
        }

        public TResponse RunRequest(string requestPath, int timeoutMs) {
            return Run("--request " + Quote(requestPath), timeoutMs);
        }

        private TResponse Run(string arguments, int timeoutMs) {
            var startInfo = new ProcessStartInfo {
                FileName = workerPath,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(workerPath)
            };

            try {
                using (Process process = Process.Start(startInfo)) {
                    if (process == null) {
                        return failedResponseFactory($"Failed to start {workerName} worker.");
                    }

                    WorkerProcessOutput output = WaitForProcess(process, Math.Max(1, timeoutMs));
                    if (output.TimedOut) {
                        return failedResponseFactory($"{workerName} worker timed out after {timeoutMs} ms.");
                    }

                    if (string.IsNullOrWhiteSpace(output.StandardOutput)) {
                        return failedResponseFactory(
                            string.IsNullOrWhiteSpace(output.StandardError)
                                ? $"{workerName} worker returned no output."
                                : FirstLine(output.StandardError));
                    }

                    try {
                        return JsonConvert.DeserializeObject<TResponse>(output.StandardOutput);
                    } catch (JsonException) {
                        return failedResponseFactory(FirstLine(output.StandardOutput));
                    }
                }
            } catch (Exception ex) {
                return failedResponseFactory($"{workerName} worker failed: {ex.Message}");
            }
        }

        private static WorkerProcessOutput WaitForProcess(Process process, int timeoutMs) {
            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();

            using (var outputClosed = new ManualResetEvent(false))
            using (var errorClosed = new ManualResetEvent(false)) {
                process.OutputDataReceived += (sender, e) => {
                    if (e.Data == null) {
                        outputClosed.Set();
                    } else {
                        standardOutput.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) => {
                    if (e.Data == null) {
                        errorClosed.Set();
                    } else {
                        standardError.AppendLine(e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs)) {
                    TryKill(process);
                    return new WorkerProcessOutput {
                        TimedOut = true,
                        StandardOutput = standardOutput.ToString(),
                        StandardError = standardError.ToString()
                    };
                }

                outputClosed.WaitOne(100);
                errorClosed.WaitOne(100);
            }

            return new WorkerProcessOutput {
                StandardOutput = standardOutput.ToString(),
                StandardError = standardError.ToString()
            };
        }

        private static void TryKill(Process process) {
            try {
                if (process != null && !process.HasExited) {
                    process.Kill();
                }
            } catch {
            }
        }

        private static string Quote(string value) {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string FirstLine(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return "Worker failed.";
            }

            using (var reader = new StringReader(value.Trim())) {
                string line = reader.ReadLine();
                return string.IsNullOrWhiteSpace(line) ? "Worker failed." : line.Trim();
            }
        }

        private sealed class WorkerProcessOutput {
            public bool TimedOut { get; set; }
            public string StandardOutput { get; set; }
            public string StandardError { get; set; }
        }
    }
}
