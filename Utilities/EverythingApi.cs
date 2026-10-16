using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WinFinder.Utilities {
    /// <summary>
    /// Thin wrapper over the Everything SDK. WinFinder bundles Everything64.dll and can also start
    /// a bundled Everything runtime from .\Everything\Everything.exe when the IPC server is absent.
    /// </summary>
    public static class EverythingApi {
        private const string EverythingDll = "Everything64.dll";
        private const string EverythingExe = "Everything.exe";

        private static readonly string[] DllCandidatePaths = new[] {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EverythingDll),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", EverythingDll),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", EverythingDll),
            EverythingDll
        };

        private static readonly object SyncRoot = new object();
        private static bool _loaded;
        private static bool _runtimeStartAttempted;

        public static bool IsAvailable => EnsureReady(out _);
        public static bool IsSdkAvailable => EnsureLoaded(out _);

        public static bool EnsureReady(out string error) {
            lock (SyncRoot) {
                if (!EnsureLoaded(out error)) {
                    return false;
                }

                if (ProbeIpc()) {
                    error = string.Empty;
                    return true;
                }

                TryStartRuntimeOnce();
                for (int i = 0; i < 20; i++) {
                    Thread.Sleep(150);
                    if (ProbeIpc()) {
                        error = string.Empty;
                        return true;
                    }
                }

                error = "Everything IPC server is not available. The bundled or installed Everything runtime may still be starting or indexing.";
                return false;
            }
        }

        public static bool EnsureLoaded(out string error) {
            lock (SyncRoot) {
                if (_loaded) {
                    error = string.Empty;
                    return true;
                }

                foreach (var path in DllCandidatePaths) {
                    try {
                        if (!File.Exists(path) && !string.Equals(path, EverythingDll, StringComparison.OrdinalIgnoreCase)) {
                            continue;
                        }

                        var handle = NativeMethods.LoadLibrary(path);
                        if (handle != IntPtr.Zero) {
                            _loaded = true;
                            error = string.Empty;
                            return true;
                        }
                    } catch {
                        // Try the next candidate.
                    }
                }

                error = "Everything64.dll not found beside the executable or in an installed Everything directory.";
                return false;
            }
        }

        public static IReadOnlyList<EverythingResult> Query(string search, int offset = 0, int maxResults = 200) {
            lock (SyncRoot) {
                if (!EnsureReady(out var loadError)) {
                    throw new InvalidOperationException(loadError);
                }

                NativeMethods.Everything_Reset();
                NativeMethods.Everything_SetSearchW(search ?? string.Empty);
                NativeMethods.Everything_SetRequestFlags(
                    (uint)(
                        EverythingRequest.FULL_PATH_AND_FILE_NAME |
                        EverythingRequest.SIZE |
                        EverythingRequest.DATE_MODIFIED |
                        EverythingRequest.ATTRIBUTES));
                NativeMethods.Everything_SetOffset((uint)Math.Max(0, offset));
                NativeMethods.Everything_SetMax((uint)Math.Max(0, maxResults));

                if (!NativeMethods.Everything_QueryW(true)) {
                    var code = (EverythingError)NativeMethods.Everything_GetLastError();
                    throw new InvalidOperationException($"Everything query failed: {code}");
                }

                int count = NativeMethods.Everything_GetNumResults();
                var results = new List<EverythingResult>(count);

                for (int i = 0; i < count; i++) {
                    string fullPath = GetResultFullPath(i);
                    if (string.IsNullOrWhiteSpace(fullPath)) {
                        continue;
                    }

                    long? size = null;
                    if (NativeMethods.Everything_IsFileResult(i) && NativeMethods.Everything_GetResultSize(i, out long value)) {
                        size = value;
                    }

                    DateTime? modified = null;
                    if (NativeMethods.Everything_GetResultDateModified(i, out NativeMethods.FILETIME ft)) {
                        long fileTime = ((long)ft.dwHighDateTime << 32) + ft.dwLowDateTime;
                        try {
                            modified = DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
                        } catch {
                            // Ignore invalid file times returned by stale results.
                        }
                    }

                    var attrs = (FileAttributes)NativeMethods.Everything_GetResultAttributes(i);
                    results.Add(new EverythingResult(fullPath, size, modified, attrs));
                }

                return results;
            }
        }

        public static IReadOnlyList<EverythingResult> QueryDirectory(string folderPath, int offset = 0, int maxResults = 10000) {
            if (string.IsNullOrWhiteSpace(folderPath)) {
                return Array.Empty<EverythingResult>();
            }

            return Query(BuildParentQuery(folderPath), offset, maxResults);
        }

        private static bool ProbeIpc() {
            try {
                NativeMethods.Everything_Reset();
                NativeMethods.Everything_SetSearchW("__winfinder_everything_probe__");
                NativeMethods.Everything_SetMax(0);
                return NativeMethods.Everything_QueryW(true);
            } catch (Exception ex) {
                Trace.WriteLine($"Everything IPC probe failed: {ex.Message}");
                return false;
            } finally {
                try {
                    NativeMethods.Everything_Reset();
                } catch {
                    // Ignore cleanup failures.
                }
            }
        }

        private static void TryStartRuntimeOnce() {
            if (_runtimeStartAttempted) {
                return;
            }

            _runtimeStartAttempted = true;

            string executable = GetEverythingExecutablePath();
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable)) {
                Trace.WriteLine("Everything runtime executable was not found.");
                return;
            }

            try {
                var startInfo = new ProcessStartInfo {
                    FileName = executable,
                    Arguments = "-startup",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(executable)
                };
                Process.Start(startInfo);
                Trace.WriteLine($"Started Everything runtime: {executable}");
            } catch (Exception ex) {
                Trace.WriteLine($"Failed to start Everything runtime '{executable}': {ex.Message}");
            }
        }

        private static string GetEverythingExecutablePath() {
            string[] candidates = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Everything", EverythingExe),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", EverythingExe),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", EverythingExe)
            };

            foreach (string candidate in candidates) {
                if (File.Exists(candidate)) {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string GetResultFullPath(int index) {
            int capacity = 512;
            while (capacity <= 32768) {
                var buffer = new StringBuilder(capacity);
                int copied = NativeMethods.Everything_GetResultFullPathNameW(index, buffer, (uint)buffer.Capacity);
                if (copied < buffer.Capacity - 1) {
                    return buffer.ToString();
                }

                capacity *= 2;
            }

            var fallback = new StringBuilder(32768);
            NativeMethods.Everything_GetResultFullPathNameW(index, fallback, (uint)fallback.Capacity);
            return fallback.ToString();
        }

        private static string BuildParentQuery(string folderPath) {
            string normalized = NormalizeFolderPath(folderPath);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return string.Empty;
            }

            if (normalized.Length == 3 && normalized[1] == ':' && normalized[2] == '\\') {
                return $"parent:{normalized}";
            }

            return $"parent:\"{normalized}\"";
        }

        private static string NormalizeFolderPath(string folderPath) {
            string path = folderPath.Trim();
            if (path.Length == 0) {
                return string.Empty;
            }

            if (path.Length == 2 && path[1] == ':') {
                path += "\\";
            }

            if (path.Length > 3) {
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            if (path.Length == 2 && path[1] == ':') {
                path += "\\";
            }

            return path;
        }

        private static class NativeMethods {
            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [StructLayout(LayoutKind.Sequential)]
            public struct FILETIME {
                public uint dwLowDateTime;
                public uint dwHighDateTime;
            }

            [DllImport(EverythingDll, CharSet = CharSet.Unicode)]
            public static extern void Everything_SetSearchW(string lpSearchString);

            [DllImport(EverythingDll)]
            public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

            [DllImport(EverythingDll)]
            public static extern void Everything_SetMax(uint dwMax);

            [DllImport(EverythingDll)]
            public static extern void Everything_SetOffset(uint dwOffset);

            [DllImport(EverythingDll)]
            public static extern bool Everything_QueryW(bool bWait);

            [DllImport(EverythingDll)]
            public static extern void Everything_Reset();

            [DllImport(EverythingDll)]
            public static extern uint Everything_GetLastError();

            [DllImport(EverythingDll)]
            public static extern int Everything_GetNumResults();

            [DllImport(EverythingDll, CharSet = CharSet.Unicode)]
            public static extern int Everything_GetResultFullPathNameW(int index, StringBuilder wPath, uint wPathSize);

            [DllImport(EverythingDll)]
            public static extern bool Everything_IsFileResult(int index);

            [DllImport(EverythingDll)]
            public static extern bool Everything_GetResultSize(int index, out long lpFileSize);

            [DllImport(EverythingDll)]
            public static extern bool Everything_GetResultDateModified(int index, out FILETIME lpFileTime);

            [DllImport(EverythingDll)]
            public static extern uint Everything_GetResultAttributes(int index);
        }
    }

    [Flags]
    public enum EverythingRequest : uint {
        FILE_NAME = 0x00000001,
        PATH = 0x00000002,
        FULL_PATH_AND_FILE_NAME = 0x00000004,
        EXTENSION = 0x00000008,
        SIZE = 0x00000010,
        DATE_CREATED = 0x00000020,
        DATE_MODIFIED = 0x00000040,
        DATE_ACCESSED = 0x00000080,
        ATTRIBUTES = 0x00000100,
        FILE_LIST_FILE_NAME = 0x00000200,
        RUN_COUNT = 0x00000400,
        DATE_RUN = 0x00000800,
        DATE_RECENTLY_CHANGED = 0x00001000,
        HIGHLIGHTED_FILE_NAME = 0x00002000,
        HIGHLIGHTED_PATH = 0x00004000,
        HIGHLIGHTED_FULL_PATH_AND_FILE_NAME = 0x00008000
    }

    public enum EverythingError {
        OK = 0,
        MEMORY = 1,
        IPC = 2,
        REGISTERCLASSEX = 3,
        CREATEWINDOW = 4,
        CREATETHREAD = 5,
        INVALIDINDEX = 6,
        INVALIDCALL = 7
    }

    public sealed class EverythingResult {
        public EverythingResult(string fullPath, long? size, DateTime? dateModified, FileAttributes attributes) {
            FullPath = fullPath;
            Size = size;
            DateModified = dateModified;
            Attributes = attributes;
        }

        public string FullPath { get; }
        public long? Size { get; }
        public DateTime? DateModified { get; }
        public FileAttributes Attributes { get; }

        public bool IsDirectory => Attributes.HasFlag(FileAttributes.Directory);
    }
}
