using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class ThumbnailCacheStore {
        private const string CacheVersion = "v1";
        private const long AccessTouchIntervalTicks = TimeSpan.TicksPerHour;
        private static readonly TimeSpan EvictionProtectionWindow = TimeSpan.FromMinutes(10);

        public const long DefaultBudgetBytes = 512L * 1024 * 1024;

        public ThumbnailCacheStore(string rootPath = null, long budgetBytes = DefaultBudgetBytes) {
            RootPath = string.IsNullOrWhiteSpace(rootPath) ? GetDefaultRootPath() : rootPath;
            BudgetBytes = budgetBytes > 0 ? budgetBytes : DefaultBudgetBytes;
        }

        public string RootPath { get; }
        public long BudgetBytes { get; }

        public CacheStatus EnsureReady() {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(Path.Combine(RootPath, "objects"));
            Directory.CreateDirectory(Path.Combine(RootPath, "failures"));

            return new CacheStatus {
                RootPath = RootPath,
                Version = CacheVersion,
                IsWritable = IsWritable(RootPath)
            };
        }

        public string GetObjectPath(string cacheKey, string extension) {
            string safeKey = string.IsNullOrWhiteSpace(cacheKey) ? "empty" : cacheKey;
            string shard = safeKey.Length >= 2 ? safeKey.Substring(0, 2) : "00";
            string fileName = safeKey + (string.IsNullOrWhiteSpace(extension) ? string.Empty : extension);
            return Path.Combine(RootPath, "objects", shard, fileName);
        }

        public string GetFailurePath(string cacheKey) {
            string safeKey = string.IsNullOrWhiteSpace(cacheKey) ? "empty" : cacheKey;
            string shard = safeKey.Length >= 2 ? safeKey.Substring(0, 2) : "00";
            return Path.Combine(RootPath, "failures", shard, safeKey + ".json");
        }

        public static void TouchMetadataAccess(string metadataPath) {
            try {
                if (string.IsNullOrWhiteSpace(metadataPath) || !File.Exists(metadataPath)) {
                    return;
                }

                ThumbnailCacheMetadata metadata = ThumbnailProtocol.Deserialize<ThumbnailCacheMetadata>(File.ReadAllText(metadataPath));
                if (metadata == null) {
                    return;
                }

                long nowTicks = DateTime.UtcNow.Ticks;
                if (metadata.LastAccessUtcTicks > 0 && nowTicks - metadata.LastAccessUtcTicks < AccessTouchIntervalTicks) {
                    return;
                }

                metadata.LastAccessUtcTicks = nowTicks;
                File.WriteAllText(metadataPath, ThumbnailProtocol.Serialize(metadata));
            } catch {
            }
        }

        public long EnforceBudget() {
            string objectsRoot = Path.Combine(RootPath, "objects");
            if (!Directory.Exists(objectsRoot)) {
                return 0;
            }

            var entries = new List<CacheObjectEntry>();
            long totalBytes = 0;
            foreach (string objectPath in Directory.EnumerateFiles(objectsRoot, "*.png", SearchOption.AllDirectories)) {
                CacheObjectEntry entry = TryDescribeObject(objectPath);
                if (entry == null) {
                    continue;
                }

                entries.Add(entry);
                totalBytes += entry.Bytes;
            }

            if (totalBytes <= BudgetBytes) {
                return 0;
            }

            long targetBytes = BudgetBytes - BudgetBytes / 10;
            long protectedTicks = DateTime.UtcNow.Subtract(EvictionProtectionWindow).Ticks;
            long evictedBytes = 0;
            foreach (CacheObjectEntry entry in entries.OrderBy(e => e.LastAccessUtcTicks)) {
                if (totalBytes <= targetBytes) {
                    break;
                }

                if (entry.ObjectWriteUtcTicks > protectedTicks) {
                    continue;
                }

                TryDeleteFile(entry.ObjectPath);
                TryDeleteFile(entry.MetadataPath);
                totalBytes -= entry.Bytes;
                evictedBytes += entry.Bytes;
            }

            return evictedBytes;
        }

        private static CacheObjectEntry TryDescribeObject(string objectPath) {
            try {
                var objectInfo = new FileInfo(objectPath);
                if (!objectInfo.Exists) {
                    return null;
                }

                string metadataPath = Path.ChangeExtension(objectPath, ".json");
                long bytes = objectInfo.Length;
                long lastAccessTicks = objectInfo.LastWriteTimeUtc.Ticks;
                if (File.Exists(metadataPath)) {
                    bytes += new FileInfo(metadataPath).Length;
                    long? metadataAccess = TryReadLastAccessTicks(metadataPath);
                    if (metadataAccess.HasValue && metadataAccess.Value > 0) {
                        lastAccessTicks = metadataAccess.Value;
                    }
                }

                return new CacheObjectEntry(objectPath, metadataPath, bytes, lastAccessTicks, objectInfo.LastWriteTimeUtc.Ticks);
            } catch {
                return null;
            }
        }

        private static long? TryReadLastAccessTicks(string metadataPath) {
            try {
                ThumbnailCacheMetadata metadata = ThumbnailProtocol.Deserialize<ThumbnailCacheMetadata>(File.ReadAllText(metadataPath));
                if (metadata == null) {
                    return null;
                }

                return metadata.LastAccessUtcTicks > 0 ? metadata.LastAccessUtcTicks : (long?)metadata.CreatedUtcTicks;
            } catch {
                return null;
            }
        }

        private static void TryDeleteFile(string path) {
            try {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
                    File.Delete(path);
                }
            } catch {
            }
        }

        private static string GetDefaultRootPath() {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData)) {
                localAppData = AppDomain.CurrentDomain.BaseDirectory;
            }

            return Path.Combine(localAppData, "WinFinder", "ThumbnailCache", CacheVersion);
        }

        private static bool IsWritable(string directory) {
            try {
                string probe = Path.Combine(directory, ".write-test");
                File.WriteAllText(probe, DateTime.UtcNow.Ticks.ToString());
                File.Delete(probe);
                return true;
            } catch {
                return false;
            }
        }

        private sealed class CacheObjectEntry {
            public CacheObjectEntry(string objectPath, string metadataPath, long bytes, long lastAccessUtcTicks, long objectWriteUtcTicks) {
                ObjectPath = objectPath;
                MetadataPath = metadataPath;
                Bytes = bytes;
                LastAccessUtcTicks = lastAccessUtcTicks;
                ObjectWriteUtcTicks = objectWriteUtcTicks;
            }

            public string ObjectPath { get; }
            public string MetadataPath { get; }
            public long Bytes { get; }
            public long LastAccessUtcTicks { get; }
            public long ObjectWriteUtcTicks { get; }
        }
    }

    internal sealed class CacheStatus {
        public string RootPath { get; set; }
        public string Version { get; set; }
        public bool IsWritable { get; set; }
        public string Diagnostic { get; set; }
    }
}
