using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WinFinder.Backend {
    public sealed class ThumbnailClientService : IThumbnailService {
        private const long ClientCacheLimitBytes = 96L * 1024 * 1024;
        private static readonly JsonSerializerSettings ProtocolJsonSettings = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly ThumbnailClientOptions options;
        private readonly IThumbnailService fallbackService;
        private readonly LruBitmapCache memoryCache = new LruBitmapCache(ClientCacheLimitBytes, StringComparer.OrdinalIgnoreCase);
        private readonly object healthSync = new object();
        private readonly object startupSync = new object();
        private DateTime nextHealthCheckUtc = DateTime.MinValue;
        private ThumbnailClientHealth lastHealth = ThumbnailClientHealth.Unavailable("Not checked.");
        private bool attemptedServiceStart;

        public ThumbnailClientService()
            : this(new ThumbnailClientOptions(), new ThumbnailService()) {
        }

        public ThumbnailClientService(ThumbnailClientOptions options, IThumbnailService fallbackService) {
            this.options = options ?? new ThumbnailClientOptions();
            this.fallbackService = fallbackService ?? new ThumbnailService();
        }

        public BitmapSource GetThumbnail(ThumbnailRequest request) {
            return GetThumbnailResult(request).Image;
        }

        public ThumbnailResult GetThumbnailResult(ThumbnailRequest request) {
            if (request == null) {
                return ThumbnailResult.Empty("Empty thumbnail request.");
            }

            if (request.CancellationToken.IsCancellationRequested) {
                return ThumbnailResult.Pending(diagnostic: "Thumbnail request was cancelled.");
            }

            ThumbnailPathIdentity identity = ThumbnailPathTools.CreateIdentity(request.FullPath);
            if (string.IsNullOrWhiteSpace(identity.CacheKey)) {
                return ThumbnailResult.Empty("Empty thumbnail path.");
            }

            string clientCacheKey = BuildClientCacheKey(identity.CacheKey, request.TargetPixels);
            if (memoryCache.TryGet(clientCacheKey, out BitmapSource cachedThumb)) {
                return ThumbnailResult.Ready(cachedThumb, ThumbnailResultSource.MemoryCache, clientCacheKey);
            }

            if (request.CancellationToken.IsCancellationRequested) {
                return ThumbnailResult.Pending(clientCacheKey, "Thumbnail request was cancelled.");
            }

            ThumbnailResult localIcon = TryGetLocalStaticIcon(request, clientCacheKey);
            if (localIcon != null) {
                return CacheAndReturn(localIcon, clientCacheKey);
            }

            if (IsRemoteServiceAvailable()) {
                ThumbnailResult remoteResult = TryGetRemoteThumbnail(request);
                if (remoteResult != null && remoteResult.Status != ThumbnailResultStatus.Empty) {
                    return CacheAndReturn(remoteResult, clientCacheKey);
                }
            }

            if (request.CancellationToken.IsCancellationRequested) {
                return ThumbnailResult.Pending(clientCacheKey, "Thumbnail request was cancelled.");
            }

            ThumbnailResult fallbackResult = fallbackService.GetThumbnailResult(request);
            return CacheAndReturn(fallbackResult, clientCacheKey);
        }

        private static ThumbnailResult TryGetLocalStaticIcon(ThumbnailRequest request, string clientCacheKey) {
            ThumbnailResult icon = StaticIconCatalog.TryGetStaticIcon(request.Format, request.IsDirectory, clientCacheKey);
            return icon != null && icon.HasImage ? icon : null;
        }

        public ThumbnailClientHealth GetHealth(bool forceRefresh = false) {
            if (!forceRefresh && DateTime.UtcNow < nextHealthCheckUtc) {
                return lastHealth;
            }

            lock (healthSync) {
                if (!forceRefresh && DateTime.UtcNow < nextHealthCheckUtc) {
                    return lastHealth;
                }

                lastHealth = ProbeHealth();
                if (!lastHealth.IsAvailable && options.StartServiceOnDemand && TryStartServiceOnce()) {
                    lastHealth = ProbeUntilAvailable(options.StartupProbeTimeoutMs, options.StartupProbeIntervalMs);
                }

                int retryMs = lastHealth.IsAvailable ? options.HealthyRetryMs : options.UnhealthyRetryMs;
                nextHealthCheckUtc = DateTime.UtcNow.AddMilliseconds(retryMs);
                return lastHealth;
            }
        }

        private ThumbnailClientHealth ProbeUntilAvailable(int timeoutMs, int intervalMs) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ThumbnailClientHealth health = lastHealth;

            do {
                if (intervalMs > 0) {
                    Thread.Sleep(intervalMs);
                }

                health = ProbeHealth();
                if (health.IsAvailable) {
                    return health;
                }
            } while (stopwatch.ElapsedMilliseconds < timeoutMs);

            return health;
        }

        private bool IsRemoteServiceAvailable() {
            return GetHealth().IsAvailable;
        }

        private ThumbnailResult TryGetRemoteThumbnail(ThumbnailRequest request) {
            if (request.CancellationToken.IsCancellationRequested) {
                return ThumbnailResult.Pending(diagnostic: "Thumbnail request was cancelled.");
            }

            try {
                using (var pipe = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.None)) {
                    int timeoutMs = Math.Max(1, Math.Min(options.RequestTimeoutMs, request.DeadlineMs));
                    pipe.Connect(options.RequestConnectTimeoutMs);
                    pipe.ReadMode = PipeTransmissionMode.Byte;

                    var clientRequest = new ThumbnailClientRequest(
                        ThumbnailClientRequestKind.Thumbnail,
                        Guid.NewGuid().ToString("N"),
                        request,
                        request.Priority,
                        timeoutMs);

                    string requestJson = JsonConvert.SerializeObject(clientRequest, ProtocolJsonSettings);
                    WriteFrame(pipe, requestJson, timeoutMs);

                    string responseJson = ReadFrame(pipe, timeoutMs);
                    var response = JsonConvert.DeserializeObject<ThumbnailResponse>(responseJson, ProtocolJsonSettings);
                    return MapRemoteThumbnailResponse(response);
                }
            } catch (TimeoutException ex) {
                Trace.WriteLine($"Remote thumbnail request timed out: {ex.Message}");
                return ThumbnailResult.Empty(ex.Message);
            } catch (IOException ex) {
                Trace.WriteLine($"Remote thumbnail request failed: {ex.Message}");
                return ThumbnailResult.Empty(ex.Message);
            } catch (UnauthorizedAccessException ex) {
                Trace.WriteLine($"Remote thumbnail request denied: {ex.Message}");
                return ThumbnailResult.Empty(ex.Message);
            } catch (Exception ex) {
                Trace.WriteLine($"Remote thumbnail request failed: {ex.Message}");
                return ThumbnailResult.Empty(ex.Message);
            }
        }

        private static ThumbnailResult MapRemoteThumbnailResponse(ThumbnailResponse response) {
            if (response == null) {
                return ThumbnailResult.Empty("Thumbnail service returned an unreadable response.");
            }

            if (response.ProtocolVersion != ThumbnailClientOptions.CurrentProtocolVersion) {
                return ThumbnailResult.Empty($"Thumbnail service protocol mismatch: {response.ProtocolVersion}.");
            }

            if (string.Equals(response.Status, "unsupported", StringComparison.OrdinalIgnoreCase)) {
                return ThumbnailResult.Empty(response.Diagnostic);
            }

            if (string.Equals(response.Status, "error", StringComparison.OrdinalIgnoreCase)) {
                return ThumbnailResult.Empty(response.Diagnostic);
            }

            if (!string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase)) {
                return ThumbnailResult.Empty(response.Diagnostic);
            }

            if (string.IsNullOrWhiteSpace(response.CachePath) || !File.Exists(response.CachePath)) {
                return ThumbnailResult.Empty("Thumbnail service returned a missing cache path.");
            }

            BitmapSource image = LoadBitmapFromFile(response.CachePath);
            if (image == null) {
                return ThumbnailResult.Empty("Failed to load thumbnail service cache image.");
            }

            ThumbnailResultSource source = string.Equals(response.Source, "cache", StringComparison.OrdinalIgnoreCase)
                ? ThumbnailResultSource.ServiceCache
                : ThumbnailResultSource.ServiceGenerated;
            return ThumbnailResult.Ready(image, source, response.CacheKey, response.Diagnostic);
        }

        private static BitmapSource LoadBitmapFromFile(string path) {
            try {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Failed to load remote thumbnail '{path}': {ex.Message}");
                return null;
            }
        }

        private bool TryStartServiceOnce() {
            lock (startupSync) {
                if (attemptedServiceStart) {
                    return false;
                }
                attemptedServiceStart = true;
            }

            string servicePath = GetServiceExecutablePath();
            if (string.IsNullOrWhiteSpace(servicePath) || !File.Exists(servicePath)) {
                Trace.WriteLine($"Thumbnail service executable not found: {servicePath}");
                return false;
            }

            try {
                var startInfo = new ProcessStartInfo {
                    FileName = servicePath,
                    Arguments = $"--pipe {QuoteArgument(options.PipeName)}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(servicePath)
                };
                Process.Start(startInfo);
                Trace.WriteLine($"Started thumbnail service: {servicePath}");
                return true;
            } catch (Exception ex) {
                Trace.WriteLine($"Failed to start thumbnail service '{servicePath}': {ex.Message}");
                return false;
            }
        }

        private static string GetServiceExecutablePath() {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(baseDirectory, "Thumbnailer", "WinFinder.Thumbnailer.Service.exe");
            if (File.Exists(candidate)) {
                return candidate;
            }

            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = string.IsNullOrWhiteSpace(assemblyLocation)
                ? baseDirectory
                : Path.GetDirectoryName(assemblyLocation);
            return Path.Combine(assemblyDirectory ?? baseDirectory, "Thumbnailer", "WinFinder.Thumbnailer.Service.exe");
        }

        private static string QuoteArgument(string value) {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private ThumbnailResult CacheAndReturn(ThumbnailResult result, string clientCacheKey) {
            if (result == null || !result.HasImage || string.IsNullOrWhiteSpace(clientCacheKey)) {
                return result;
            }

            BitmapSource image = result.Image;
            if (image.CanFreeze) {
                image.Freeze();
            }

            memoryCache.AddOrUpdate(clientCacheKey, image);
            if (string.Equals(result.CacheKey, clientCacheKey, StringComparison.OrdinalIgnoreCase)) {
                return result;
            }

            return result.Status == ThumbnailResultStatus.Fallback
                ? ThumbnailResult.Fallback(image, result.Source, clientCacheKey, result.Diagnostic)
                : ThumbnailResult.Ready(image, result.Source, clientCacheKey, result.Diagnostic);
        }

        private static string BuildClientCacheKey(string identityCacheKey, int targetPixels) {
            return string.Join("|", identityCacheKey ?? string.Empty, Math.Max(1, targetPixels).ToString());
        }

        private ThumbnailClientHealth ProbeHealth() {
            try {
                using (var pipe = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.None)) {
                    pipe.Connect(options.ConnectTimeoutMs);
                    pipe.ReadMode = PipeTransmissionMode.Byte;

                    string requestJson = JsonConvert.SerializeObject(ThumbnailClientRequest.Health(), ProtocolJsonSettings);
                    WriteFrame(pipe, requestJson, options.RequestTimeoutMs);

                    string responseJson = ReadFrame(pipe, options.RequestTimeoutMs);
                    if (string.IsNullOrWhiteSpace(responseJson)) {
                        return ThumbnailClientHealth.Unavailable("Thumbnail service returned an empty health response.");
                    }

                    var response = JsonConvert.DeserializeObject<HealthResponse>(responseJson, ProtocolJsonSettings);
                    if (response == null) {
                        return ThumbnailClientHealth.Unavailable("Thumbnail service returned an unreadable health response.");
                    }

                    if (response.ProtocolVersion != ThumbnailClientOptions.CurrentProtocolVersion) {
                        return ThumbnailClientHealth.Unavailable($"Thumbnail service protocol mismatch: {response.ProtocolVersion}.");
                    }

                    return response.IsHealthy
                        ? ThumbnailClientHealth.Available(response.Diagnostic)
                        : ThumbnailClientHealth.Unavailable(response.Diagnostic);
                }
            } catch (TimeoutException ex) {
                Trace.WriteLine($"Thumbnail service health probe timed out: {ex.Message}");
                return ThumbnailClientHealth.Unavailable(ex.Message);
            } catch (IOException ex) {
                Trace.WriteLine($"Thumbnail service health probe failed: {ex.Message}");
                return ThumbnailClientHealth.Unavailable(ex.Message);
            } catch (UnauthorizedAccessException ex) {
                Trace.WriteLine($"Thumbnail service health probe denied: {ex.Message}");
                return ThumbnailClientHealth.Unavailable(ex.Message);
            } catch (Exception ex) {
                Trace.WriteLine($"Thumbnail service health probe failed: {ex.Message}");
                return ThumbnailClientHealth.Unavailable(ex.Message);
            }
        }

        private static void WriteFrame(Stream stream, string payload, int timeoutMs) {
            byte[] data = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            byte[] length = BitConverter.GetBytes(data.Length);
            WriteWithTimeout(stream, length, timeoutMs);
            if (data.Length > 0) {
                WriteWithTimeout(stream, data, timeoutMs);
            }
            stream.Flush();
        }

        private static string ReadFrame(Stream stream, int timeoutMs) {
            byte[] lengthBytes = ReadExact(stream, sizeof(int), timeoutMs);
            int length = BitConverter.ToInt32(lengthBytes, 0);
            if (length <= 0 || length > 1024 * 1024) {
                return string.Empty;
            }

            byte[] payload = ReadExact(stream, length, timeoutMs);
            return Encoding.UTF8.GetString(payload);
        }

        private static byte[] ReadExact(Stream stream, int length, int timeoutMs) {
            var buffer = new byte[length];
            int offset = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (offset < length) {
                if (stopwatch.ElapsedMilliseconds > timeoutMs) {
                    throw new TimeoutException("Timed out waiting for thumbnail service response.");
                }

                int remainingMs = timeoutMs - (int)stopwatch.ElapsedMilliseconds;
                if (remainingMs <= 0) {
                    throw new TimeoutException("Timed out waiting for thumbnail service response.");
                }

                Task<int> readTask = stream.ReadAsync(buffer, offset, length - offset);
                if (!readTask.Wait(remainingMs)) {
                    throw new TimeoutException("Timed out waiting for thumbnail service response.");
                }

                int read = readTask.Result;
                if (read <= 0) {
                    throw new IOException("Thumbnail service pipe disconnected.");
                }
                offset += read;
            }

            return buffer;
        }

        private static void WriteWithTimeout(Stream stream, byte[] data, int timeoutMs) {
            Task writeTask = stream.WriteAsync(data, 0, data.Length);
            if (!writeTask.Wait(timeoutMs)) {
                throw new TimeoutException("Timed out writing to thumbnail service.");
            }
        }

        private sealed class HealthResponse {
            public int ProtocolVersion { get; set; }
            public bool IsHealthy { get; set; }
            public string Diagnostic { get; set; }
        }

        private sealed class ThumbnailResponse {
            public int ProtocolVersion { get; set; }
            public string RequestId { get; set; }
            public string Status { get; set; }
            public string Source { get; set; }
            public string DecoderId { get; set; }
            public string MimeType { get; set; }
            public string CacheKey { get; set; }
            public string CachePath { get; set; }
            public string Diagnostic { get; set; }
        }
    }
}
