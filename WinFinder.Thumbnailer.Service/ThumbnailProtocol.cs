using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WinFinder.Thumbnailer.Service {
    internal static class ThumbnailProtocol {
        public const int CurrentProtocolVersion = 1;
        public const int HealthKind = 0;
        public const int CapabilitiesKind = 1;
        public const int ThumbnailKind = 2;
        private static readonly DateTime StartedUtc = DateTime.UtcNow;
        private static ThumbnailCacheStore cacheStore = new ThumbnailCacheStore();
        private static ThumbnailGenerator generator = new ThumbnailGenerator(cacheStore);

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string Handle(string requestJson) {
            ProtocolRequest request;
            try {
                request = JsonConvert.DeserializeObject<ProtocolRequest>(requestJson, JsonSettings);
            } catch (JsonException ex) {
                return SerializeHealth(null, false, $"Invalid request JSON: {ex.Message}");
            }

            if (request == null) {
                return SerializeHealth(null, false, "Empty thumbnailer request.");
            }

            if (request.ProtocolVersion != CurrentProtocolVersion) {
                return SerializeHealth(request.RequestId, false, $"Protocol mismatch: {request.ProtocolVersion}.");
            }

            if (request.Kind == ThumbnailKind) {
                return Serialize(generator.Generate(request));
            }

            if (request.Kind == CapabilitiesKind) {
                return Serialize(new CapabilitiesResponse {
                    ProtocolVersion = CurrentProtocolVersion,
                    RequestId = request.RequestId,
                    IsHealthy = true,
                    Cache = GetCacheStatus(),
                    Capabilities = ThumbnailCapabilities.Current()
                });
            }

            if (request.Kind != HealthKind) {
                return SerializeHealth(request.RequestId, false, $"Unsupported request kind: {request.Kind}.");
            }

            return SerializeHealth(
                request.RequestId,
                true,
                $"WinFinder thumbnailer service is running. pid={Process.GetCurrentProcess().Id}.");
        }

        public static void ConfigureCache(string rootPath, long budgetBytes) {
            cacheStore = new ThumbnailCacheStore(
                string.IsNullOrWhiteSpace(rootPath) ? null : rootPath,
                budgetBytes > 0 ? budgetBytes : ThumbnailCacheStore.DefaultBudgetBytes);
            generator = new ThumbnailGenerator(cacheStore);
        }

        public static void RunCacheMaintenance() {
            try {
                cacheStore.EnforceBudget();
            } catch {
            }
        }

        public static ProtocolRequest CreateHealthRequest() {
            return new ProtocolRequest {
                ProtocolVersion = CurrentProtocolVersion,
                Kind = HealthKind,
                RequestId = Guid.NewGuid().ToString("N"),
                Priority = 100,
                DeadlineMs = 250
            };
        }

        public static ProtocolRequest CreateThumbnailRequest(string path, string name, string format, int targetPixels) {
            return new ProtocolRequest {
                ProtocolVersion = CurrentProtocolVersion,
                Kind = ThumbnailKind,
                RequestId = Guid.NewGuid().ToString("N"),
                Priority = 50,
                DeadlineMs = 4000,
                Thumbnail = new ProtocolThumbnailRequest {
                    FullPath = path,
                    Name = name,
                    Format = format,
                    IsDirectory = false,
                    TargetPixels = targetPixels <= 0 ? 210 : targetPixels
                }
            };
        }

        public static T Deserialize<T>(string json) {
            return JsonConvert.DeserializeObject<T>(json, JsonSettings);
        }

        public static string Serialize(object value) {
            return JsonConvert.SerializeObject(value, JsonSettings);
        }

        private static string SerializeHealth(string requestId, bool isHealthy, string diagnostic) {
            return Serialize(new HealthResponse {
                ProtocolVersion = CurrentProtocolVersion,
                RequestId = requestId,
                IsHealthy = isHealthy,
                Diagnostic = diagnostic,
                UptimeSeconds = (long)(DateTime.UtcNow - StartedUtc).TotalSeconds,
                Cache = GetCacheStatus(),
                Capabilities = ThumbnailCapabilities.Current()
            });
        }

        private static CacheStatus GetCacheStatus() {
            try {
                return cacheStore.EnsureReady();
            } catch {
                return new CacheStatus {
                    RootPath = cacheStore.RootPath,
                    Version = "v1",
                    IsWritable = false
                };
            }
        }
    }

    internal sealed class ProtocolRequest {
        public int ProtocolVersion { get; set; }
        public int Kind { get; set; }
        public string RequestId { get; set; }
        public ProtocolThumbnailRequest Thumbnail { get; set; }
        public int Priority { get; set; }
        public int DeadlineMs { get; set; }
    }

    internal sealed class HealthResponse {
        public int ProtocolVersion { get; set; }
        public string RequestId { get; set; }
        public bool IsHealthy { get; set; }
        public string Diagnostic { get; set; }
        public long UptimeSeconds { get; set; }
        public CacheStatus Cache { get; set; }
        public ThumbnailCapabilities Capabilities { get; set; }
    }

    internal sealed class CapabilitiesResponse {
        public int ProtocolVersion { get; set; }
        public string RequestId { get; set; }
        public bool IsHealthy { get; set; }
        public CacheStatus Cache { get; set; }
        public ThumbnailCapabilities Capabilities { get; set; }
    }

    internal sealed class ThumbnailCapabilities {
        public bool Text { get; set; }
        public bool Pdf { get; set; }
        public bool Svg { get; set; }
        public bool ShellFallback { get; set; }
        public bool Video { get; set; }
        public bool Heic { get; set; }
        public bool Avif { get; set; }
        public bool Raw { get; set; }

        public static ThumbnailCapabilities Current() {
            ImageWorkerFormatCapabilities imageCapabilities = ImageThumbnailRenderer.Capabilities;
            return new ThumbnailCapabilities {
                Text = true,
                Pdf = PdfThumbnailRenderer.IsAvailable,
                Svg = SvgThumbnailRenderer.IsAvailable,
                ShellFallback = ShellThumbnailRenderer.IsAvailable,
                Video = VideoThumbnailRenderer.IsAvailable,
                Heic = imageCapabilities.Heic || imageCapabilities.Heif,
                Avif = imageCapabilities.Avif,
                Raw = imageCapabilities.Raw
            };
        }
    }
}
