using System;

namespace WinFinder.Backend {
    public enum ThumbnailClientRequestKind {
        Health = 0,
        Capabilities = 1,
        Thumbnail = 2
    }

    public sealed class ThumbnailClientOptions {
        public const int CurrentProtocolVersion = 1;
        public const string DefaultPipeName = "WinFinder.Thumbnailer.v1";

        public ThumbnailClientOptions(
            string pipeName = DefaultPipeName,
            int connectTimeoutMs = 25,
            int requestConnectTimeoutMs = 600,
            int requestTimeoutMs = 4000,
            int unhealthyRetryMs = 30000,
            int healthyRetryMs = 5000,
            bool startServiceOnDemand = true,
            int startupProbeTimeoutMs = 900,
            int startupProbeIntervalMs = 90) {
            PipeName = string.IsNullOrWhiteSpace(pipeName) ? DefaultPipeName : pipeName;
            ConnectTimeoutMs = Math.Max(1, connectTimeoutMs);
            RequestConnectTimeoutMs = Math.Max(1, requestConnectTimeoutMs);
            RequestTimeoutMs = Math.Max(1, requestTimeoutMs);
            UnhealthyRetryMs = Math.Max(1000, unhealthyRetryMs);
            HealthyRetryMs = Math.Max(1000, healthyRetryMs);
            StartServiceOnDemand = startServiceOnDemand;
            StartupProbeTimeoutMs = Math.Max(0, startupProbeTimeoutMs);
            StartupProbeIntervalMs = Math.Max(10, startupProbeIntervalMs);
        }

        public string PipeName { get; }
        public int ConnectTimeoutMs { get; }
        public int RequestConnectTimeoutMs { get; }
        public int RequestTimeoutMs { get; }
        public int UnhealthyRetryMs { get; }
        public int HealthyRetryMs { get; }
        public bool StartServiceOnDemand { get; }
        public int StartupProbeTimeoutMs { get; }
        public int StartupProbeIntervalMs { get; }
    }

    public sealed class ThumbnailClientRequest {
        public ThumbnailClientRequest(
            ThumbnailClientRequestKind kind,
            string requestId,
            ThumbnailRequest thumbnail,
            ThumbnailPriority priority,
            int deadlineMs) {
            Kind = kind;
            RequestId = requestId ?? Guid.NewGuid().ToString("N");
            Thumbnail = thumbnail;
            Priority = priority;
            DeadlineMs = Math.Max(1, deadlineMs);
            ProtocolVersion = ThumbnailClientOptions.CurrentProtocolVersion;
        }

        public int ProtocolVersion { get; }
        public ThumbnailClientRequestKind Kind { get; }
        public string RequestId { get; }
        public ThumbnailRequest Thumbnail { get; }
        public ThumbnailPriority Priority { get; }
        public int DeadlineMs { get; }

        public static ThumbnailClientRequest Health() {
            return new ThumbnailClientRequest(ThumbnailClientRequestKind.Health, Guid.NewGuid().ToString("N"), null, ThumbnailPriority.Immediate, 250);
        }
    }

    public sealed class ThumbnailClientHealth {
        private ThumbnailClientHealth(bool isAvailable, string diagnostic) {
            IsAvailable = isAvailable;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool IsAvailable { get; }
        public string Diagnostic { get; }

        public static ThumbnailClientHealth Available(string diagnostic = null) {
            return new ThumbnailClientHealth(true, diagnostic);
        }

        public static ThumbnailClientHealth Unavailable(string diagnostic = null) {
            return new ThumbnailClientHealth(false, diagnostic);
        }
    }
}
