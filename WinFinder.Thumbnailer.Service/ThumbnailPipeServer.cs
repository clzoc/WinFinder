using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class ThumbnailPipeServer {
        internal const int RequestTimeoutMs = 5000;
        internal const int DefaultMaxConcurrentConnections = 4;
        private readonly string pipeName;

        public ThumbnailPipeServer(string pipeName) {
            this.pipeName = string.IsNullOrWhiteSpace(pipeName) ? "WinFinder.Thumbnailer.v1" : pipeName;
        }

        public void Run(CancellationToken cancellationToken) {
            Run(cancellationToken, DefaultMaxConcurrentConnections);
        }

        public void Run(CancellationToken cancellationToken, int maxConcurrentConnections) {
            int maxConcurrent = Math.Max(1, Math.Min(maxConcurrentConnections, 16));
            using (var capacity = new SemaphoreSlim(maxConcurrent, maxConcurrent)) {
                while (!cancellationToken.IsCancellationRequested) {
                    capacity.Wait(cancellationToken);
                    NamedPipeServerStream pipe = null;
                    try {
                        pipe = CreatePipe(maxConcurrent);
                        WaitForConnection(pipe, cancellationToken);

                        NamedPipeServerStream connectedPipe = pipe;
                        pipe = null;
                        Task.Run(() => ProcessConnectionAsync(connectedPipe, capacity, cancellationToken));
                    } catch (OperationCanceledException) {
                        DisposeQuietly(pipe);
                        capacity.Release();
                        throw;
                    } catch (Exception ex) {
                        DisposeQuietly(pipe);
                        capacity.Release();
                        Trace.WriteLine($"Thumbnailer pipe accept failed: {ex.Message}");
                        cancellationToken.WaitHandle.WaitOne(50);
                    }
                }
            }
        }

        public void RunOnce(CancellationToken cancellationToken) {
            using (var pipe = CreatePipe(1)) {
                WaitForConnection(pipe, cancellationToken);
                ProcessConnection(pipe, cancellationToken);
            }
        }

        private static void ProcessConnectionAsync(NamedPipeServerStream pipe, SemaphoreSlim capacity, CancellationToken cancellationToken) {
            try {
                ProcessConnection(pipe, cancellationToken);
            } catch (Exception ex) {
                Trace.WriteLine($"Thumbnailer connection failed: {ex.Message}");
            } finally {
                DisposeQuietly(pipe);
                try {
                    capacity.Release();
                } catch (ObjectDisposedException) {
                }
            }
        }

        private static void ProcessConnection(NamedPipeServerStream pipe, CancellationToken cancellationToken) {
            string requestJson = PipeFrames.ReadFrame(pipe, RequestTimeoutMs, cancellationToken);
            string responseJson = ThumbnailProtocol.Handle(requestJson);
            PipeFrames.WriteFrame(pipe, responseJson, RequestTimeoutMs, cancellationToken);
            pipe.WaitForPipeDrain();
        }

        private NamedPipeServerStream CreatePipe(int maxInstances) {
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                Math.Max(1, maxInstances),
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                4096,
                4096,
                CreatePipeSecurity());
        }

        private static void DisposeQuietly(NamedPipeServerStream pipe) {
            try {
                pipe?.Dispose();
            } catch {
            }
        }

        private static PipeSecurity CreatePipeSecurity() {
            var security = new PipeSecurity();
            SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null) {
                security.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
            }

            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
                AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            return security;
        }

        private static void WaitForConnection(NamedPipeServerStream pipe, CancellationToken cancellationToken) {
            Task connectTask = pipe.WaitForConnectionAsync(cancellationToken);
            WaitTask(connectTask, Timeout.Infinite, cancellationToken);
        }

        internal static void WaitTask(Task task, int timeoutMs, CancellationToken cancellationToken) {
            try {
                bool completed = timeoutMs == Timeout.Infinite
                    ? task.Wait(Timeout.Infinite, cancellationToken)
                    : task.Wait(timeoutMs, cancellationToken);

                if (!completed) {
                    throw new TimeoutException("Timed out waiting for thumbnailer pipe I/O.");
                }
            } catch (AggregateException ex) {
                throw ex.Flatten().InnerException ?? ex;
            }
        }

        internal static T WaitTask<T>(Task<T> task, int timeoutMs, CancellationToken cancellationToken) {
            WaitTask((Task)task, timeoutMs, cancellationToken);
            return task.Result;
        }
    }

    internal static class PipeFrames {
        private const int MaxFrameBytes = 1024 * 1024;

        public static void WriteFrame(Stream stream, string payload, int timeoutMs, CancellationToken cancellationToken) {
            byte[] data = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            byte[] length = BitConverter.GetBytes(data.Length);
            WriteWithTimeout(stream, length, timeoutMs, cancellationToken);
            if (data.Length > 0) {
                WriteWithTimeout(stream, data, timeoutMs, cancellationToken);
            }
            stream.Flush();
        }

        public static string ReadFrame(Stream stream, int timeoutMs, CancellationToken cancellationToken) {
            byte[] lengthBytes = ReadExact(stream, sizeof(int), timeoutMs, cancellationToken);
            int length = BitConverter.ToInt32(lengthBytes, 0);
            if (length <= 0 || length > MaxFrameBytes) {
                throw new IOException($"Invalid thumbnailer frame length: {length}.");
            }

            byte[] payload = ReadExact(stream, length, timeoutMs, cancellationToken);
            return Encoding.UTF8.GetString(payload);
        }

        private static byte[] ReadExact(Stream stream, int length, int timeoutMs, CancellationToken cancellationToken) {
            var buffer = new byte[length];
            int offset = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (offset < length) {
                cancellationToken.ThrowIfCancellationRequested();
                int remainingMs = timeoutMs - (int)stopwatch.ElapsedMilliseconds;
                if (remainingMs <= 0) {
                    throw new TimeoutException("Timed out waiting for thumbnailer request.");
                }

                Task<int> readTask = stream.ReadAsync(buffer, offset, length - offset, cancellationToken);
                int read = ThumbnailPipeServer.WaitTask(readTask, remainingMs, cancellationToken);
                if (read <= 0) {
                    throw new IOException("Thumbnailer pipe disconnected.");
                }
                offset += read;
            }

            return buffer;
        }

        private static void WriteWithTimeout(Stream stream, byte[] data, int timeoutMs, CancellationToken cancellationToken) {
            Task writeTask = stream.WriteAsync(data, 0, data.Length, cancellationToken);
            ThumbnailPipeServer.WaitTask(writeTask, timeoutMs, cancellationToken);
        }
    }

    internal static class ThumbnailPipeSelfTest {
        public static int Run(string basePipeName, bool includeThumbnail) {
            string pipeName = $"{basePipeName}.SelfTest.{Guid.NewGuid():N}";
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5))) {
                var server = new ThumbnailPipeServer(pipeName);
                Task serverTask = Task.Run(() => {
                    if (includeThumbnail) {
                        server.RunOnce(cancellation.Token);
                        server.RunOnce(cancellation.Token);
                    } else {
                        server.RunOnce(cancellation.Token);
                    }
                }, cancellation.Token);

                try {
                    string healthJson = Exchange(pipeName, ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateHealthRequest()), cancellation.Token);
                    HealthResponse health = ThumbnailProtocol.Deserialize<HealthResponse>(healthJson);
                    Console.WriteLine(healthJson);
                    if (health == null || !health.IsHealthy) {
                        return 1;
                    }

                    if (includeThumbnail) {
                        string tempFile = Path.Combine(Path.GetTempPath(), "WinFinder.Thumbnailer.SelfTest.txt");
                        File.WriteAllText(tempFile, "WinFinder thumbnailer self test" + Environment.NewLine + DateTime.UtcNow.ToString("O"));

                        string thumbnailJson = Exchange(
                            pipeName,
                            ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateThumbnailRequest(tempFile, Path.GetFileName(tempFile), "TXT", 210)),
                            cancellation.Token);

                        ThumbnailResponse thumbnail = ThumbnailProtocol.Deserialize<ThumbnailResponse>(thumbnailJson);
                        Console.WriteLine(thumbnailJson);
                        return thumbnail != null
                            && string.Equals(thumbnail.Status, "ready", StringComparison.OrdinalIgnoreCase)
                            && File.Exists(thumbnail.CachePath)
                                ? 0
                                : 1;
                    }

                    serverTask.Wait(1000);
                    return 0;
                } catch (Exception ex) {
                    cancellation.Cancel();
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }

        public static int RunPdf(string basePipeName, string pdfPath) {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) {
                Console.Error.WriteLine($"PDF self-test file does not exist: {pdfPath}");
                return 1;
            }

            string pipeName = $"{basePipeName}.SelfTest.{Guid.NewGuid():N}";
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
                var server = new ThumbnailPipeServer(pipeName);
                Task serverTask = Task.Run(() => {
                    server.RunOnce(cancellation.Token);
                    server.RunOnce(cancellation.Token);
                }, cancellation.Token);

                try {
                    string healthJson = Exchange(pipeName, ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateHealthRequest()), cancellation.Token);
                    HealthResponse health = ThumbnailProtocol.Deserialize<HealthResponse>(healthJson);
                    Console.WriteLine(healthJson);
                    if (health == null || !health.IsHealthy || health.Capabilities == null || !health.Capabilities.Pdf) {
                        return 1;
                    }

                    string thumbnailJson = Exchange(
                        pipeName,
                        ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateThumbnailRequest(pdfPath, Path.GetFileName(pdfPath), "PDF", 210)),
                        cancellation.Token);

                    ThumbnailResponse thumbnail = ThumbnailProtocol.Deserialize<ThumbnailResponse>(thumbnailJson);
                    Console.WriteLine(thumbnailJson);
                    serverTask.Wait(1000);
                    return thumbnail != null
                        && string.Equals(thumbnail.Status, "ready", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(thumbnail.DecoderId, "pdfium", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(thumbnail.CachePath)
                            ? 0
                            : 1;
                } catch (Exception ex) {
                    cancellation.Cancel();
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }

        public static int RunSvg(string basePipeName, string svgPath) {
            if (string.IsNullOrWhiteSpace(svgPath) || !File.Exists(svgPath)) {
                Console.Error.WriteLine($"SVG self-test file does not exist: {svgPath}");
                return 1;
            }

            string pipeName = $"{basePipeName}.SelfTest.{Guid.NewGuid():N}";
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
                var server = new ThumbnailPipeServer(pipeName);
                Task serverTask = Task.Run(() => {
                    server.RunOnce(cancellation.Token);
                    server.RunOnce(cancellation.Token);
                }, cancellation.Token);

                try {
                    string healthJson = Exchange(pipeName, ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateHealthRequest()), cancellation.Token);
                    HealthResponse health = ThumbnailProtocol.Deserialize<HealthResponse>(healthJson);
                    Console.WriteLine(healthJson);
                    if (health == null || !health.IsHealthy || health.Capabilities == null || !health.Capabilities.Svg) {
                        return 1;
                    }

                    string thumbnailJson = Exchange(
                        pipeName,
                        ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateThumbnailRequest(svgPath, Path.GetFileName(svgPath), "SVG", 210)),
                        cancellation.Token);

                    ThumbnailResponse thumbnail = ThumbnailProtocol.Deserialize<ThumbnailResponse>(thumbnailJson);
                    Console.WriteLine(thumbnailJson);
                    serverTask.Wait(1000);
                    return thumbnail != null
                        && string.Equals(thumbnail.Status, "ready", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(thumbnail.DecoderId, "svg", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(thumbnail.CachePath)
                            ? 0
                            : 1;
                } catch (Exception ex) {
                    cancellation.Cancel();
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }

        public static int RunShell(string basePipeName, string filePath) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                Console.Error.WriteLine($"Shell self-test file does not exist: {filePath}");
                return 1;
            }

            string pipeName = $"{basePipeName}.SelfTest.{Guid.NewGuid():N}";
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
                var server = new ThumbnailPipeServer(pipeName);
                Task serverTask = Task.Run(() => {
                    server.RunOnce(cancellation.Token);
                    server.RunOnce(cancellation.Token);
                }, cancellation.Token);

                try {
                    string healthJson = Exchange(pipeName, ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateHealthRequest()), cancellation.Token);
                    HealthResponse health = ThumbnailProtocol.Deserialize<HealthResponse>(healthJson);
                    Console.WriteLine(healthJson);
                    if (health == null || !health.IsHealthy || health.Capabilities == null || !health.Capabilities.ShellFallback) {
                        return 1;
                    }

                    string format = Path.GetExtension(filePath);
                    string thumbnailJson = Exchange(
                        pipeName,
                        ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateThumbnailRequest(filePath, Path.GetFileName(filePath), format, 210)),
                        cancellation.Token);

                    ThumbnailResponse thumbnail = ThumbnailProtocol.Deserialize<ThumbnailResponse>(thumbnailJson);
                    Console.WriteLine(thumbnailJson);
                    serverTask.Wait(1000);
                    return thumbnail != null
                        && string.Equals(thumbnail.Status, "ready", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(thumbnail.DecoderId, "shell", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(thumbnail.CachePath)
                            ? 0
                            : 1;
                } catch (Exception ex) {
                    cancellation.Cancel();
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }

        public static int RunVideo(string basePipeName, string videoPath) {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath)) {
                Console.Error.WriteLine($"Video self-test file does not exist: {videoPath}");
                return 1;
            }

            string pipeName = $"{basePipeName}.SelfTest.{Guid.NewGuid():N}";
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
                var server = new ThumbnailPipeServer(pipeName);
                Task serverTask = Task.Run(() => {
                    server.RunOnce(cancellation.Token);
                    server.RunOnce(cancellation.Token);
                }, cancellation.Token);

                try {
                    string healthJson = Exchange(pipeName, ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateHealthRequest()), cancellation.Token);
                    HealthResponse health = ThumbnailProtocol.Deserialize<HealthResponse>(healthJson);
                    Console.WriteLine(healthJson);
                    if (health == null || !health.IsHealthy || health.Capabilities == null || !health.Capabilities.Video) {
                        return 1;
                    }

                    string format = Path.GetExtension(videoPath);
                    string thumbnailJson = Exchange(
                        pipeName,
                        ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateThumbnailRequest(videoPath, Path.GetFileName(videoPath), format, 210)),
                        cancellation.Token);

                    ThumbnailResponse thumbnail = ThumbnailProtocol.Deserialize<ThumbnailResponse>(thumbnailJson);
                    Console.WriteLine(thumbnailJson);
                    serverTask.Wait(1000);
                    return thumbnail != null
                        && string.Equals(thumbnail.Status, "ready", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(thumbnail.DecoderId, "video-ffmpeg", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(thumbnail.CachePath)
                            ? 0
                            : 1;
                } catch (Exception ex) {
                    cancellation.Cancel();
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }

        public static int RunImage(string basePipeName, string imagePath) {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) {
                Console.Error.WriteLine($"Image self-test file does not exist: {imagePath}");
                return 1;
            }

            string pipeName = $"{basePipeName}.SelfTest.{Guid.NewGuid():N}";
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
                var server = new ThumbnailPipeServer(pipeName);
                Task serverTask = Task.Run(() => {
                    server.RunOnce(cancellation.Token);
                    server.RunOnce(cancellation.Token);
                }, cancellation.Token);

                try {
                    string healthJson = Exchange(pipeName, ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateHealthRequest()), cancellation.Token);
                    HealthResponse health = ThumbnailProtocol.Deserialize<HealthResponse>(healthJson);
                    Console.WriteLine(healthJson);
                    if (health == null || !health.IsHealthy || health.Capabilities == null || (!health.Capabilities.Heic && !health.Capabilities.Avif)) {
                        return 1;
                    }

                    string format = Path.GetExtension(imagePath);
                    string thumbnailJson = Exchange(
                        pipeName,
                        ThumbnailProtocol.Serialize(ThumbnailProtocol.CreateThumbnailRequest(imagePath, Path.GetFileName(imagePath), format, 210)),
                        cancellation.Token);

                    ThumbnailResponse thumbnail = ThumbnailProtocol.Deserialize<ThumbnailResponse>(thumbnailJson);
                    Console.WriteLine(thumbnailJson);
                    serverTask.Wait(1000);
                    return thumbnail != null
                        && string.Equals(thumbnail.Status, "ready", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(thumbnail.DecoderId, "image-ffmpeg", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(thumbnail.CachePath)
                            ? 0
                            : 1;
                } catch (Exception ex) {
                    cancellation.Cancel();
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }

        private static string Exchange(string pipeName, string requestJson, CancellationToken cancellationToken) {
            using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous)) {
                client.Connect(1000);
                PipeFrames.WriteFrame(client, requestJson, ThumbnailPipeServer.RequestTimeoutMs, cancellationToken);
                return PipeFrames.ReadFrame(client, ThumbnailPipeServer.RequestTimeoutMs, cancellationToken);
            }
        }
    }
}
