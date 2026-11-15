using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WinFinder.Utilities;

namespace WinFinder.Backend {
    public enum FileTransferMode {
        Copy,
        Move
    }

    public sealed class FileClipboardState {
        public FileClipboardState(FileTransferMode mode, IReadOnlyList<string> paths) {
            Mode = mode;
            Paths = paths ?? Array.Empty<string>();
        }

        public FileTransferMode Mode { get; }
        public IReadOnlyList<string> Paths { get; }
        public bool IsEmpty => Paths == null || Paths.Count == 0;
    }

    public sealed class FileOperationResult {
        private FileOperationResult(bool success, string error, IReadOnlyList<string> affectedPaths) {
            Success = success;
            Error = error ?? string.Empty;
            AffectedPaths = affectedPaths ?? Array.Empty<string>();
        }

        public bool Success { get; }
        public string Error { get; }
        public IReadOnlyList<string> AffectedPaths { get; }

        public static FileOperationResult Ok(params string[] paths) {
            return new FileOperationResult(true, string.Empty, paths);
        }

        public static FileOperationResult Ok(IReadOnlyList<string> paths) {
            return new FileOperationResult(true, string.Empty, paths);
        }

        public static FileOperationResult Fail(string error) {
            return new FileOperationResult(false, error, Array.Empty<string>());
        }
    }

    public static class FileOperations {
        private static readonly object Sync = new object();
        private static FileClipboardState clipboard = new FileClipboardState(FileTransferMode.Copy, Array.Empty<string>());

        public static FileClipboardState ClipboardState {
            get {
                lock (Sync) {
                    return clipboard;
                }
            }
        }

        public static void SetClipboard(IReadOnlyList<string> paths, FileTransferMode mode) {
            IReadOnlyList<string> copy = NormalizeExistingPaths(paths);
            lock (Sync) {
                clipboard = new FileClipboardState(mode, copy);
            }
        }

        public static void ClearClipboard() {
            lock (Sync) {
                clipboard = new FileClipboardState(FileTransferMode.Copy, Array.Empty<string>());
            }
        }

        public static FileOperationResult Copy(IReadOnlyList<string> sources, string destinationDirectory, string sandboxRoot = null) {
            return Transfer(sources, destinationDirectory, FileTransferMode.Copy, sandboxRoot);
        }

        public static FileOperationResult Move(IReadOnlyList<string> sources, string destinationDirectory, string sandboxRoot = null) {
            return Transfer(sources, destinationDirectory, FileTransferMode.Move, sandboxRoot);
        }

        public static FileOperationResult Paste(string destinationDirectory, string sandboxRoot = null) {
            FileClipboardState state = ClipboardState;
            if (state.IsEmpty) {
                return FileOperationResult.Fail("Clipboard is empty.");
            }

            FileOperationResult result = Transfer(state.Paths, destinationDirectory, state.Mode, sandboxRoot);
            if (result.Success && state.Mode == FileTransferMode.Move) {
                ClearClipboard();
            }

            return result;
        }

        public static FileOperationResult Rename(string path, string newName, string sandboxRoot = null) {
            try {
                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(newName)) {
                    return FileOperationResult.Fail("Path and name are required.");
                }

                if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                    return FileOperationResult.Fail("The new name contains invalid characters.");
                }

                string source = Path.GetFullPath(path);
                EnsureAllowed(source, sandboxRoot);
                string directory = Path.GetDirectoryName(source);
                if (string.IsNullOrWhiteSpace(directory)) {
                    return FileOperationResult.Fail("Cannot rename this path.");
                }

                string destination = Path.Combine(directory, newName);
                EnsureAllowed(destination, sandboxRoot);
                if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) {
                    return FileOperationResult.Ok(destination);
                }

                if (File.Exists(destination) || Directory.Exists(destination)) {
                    return FileOperationResult.Fail("A file or folder with that name already exists.");
                }

                if (Directory.Exists(source)) {
                    Directory.Move(source, destination);
                } else if (File.Exists(source)) {
                    File.Move(source, destination);
                } else {
                    return FileOperationResult.Fail("The item no longer exists.");
                }

                return FileOperationResult.Ok(destination);
            } catch (Exception ex) {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        public static FileOperationResult Delete(IReadOnlyList<string> paths, bool recycle, string sandboxRoot = null) {
            try {
                IReadOnlyList<string> sources = NormalizeExistingPaths(paths);
                if (sources.Count == 0) {
                    return FileOperationResult.Fail("Nothing to delete.");
                }

                foreach (string path in sources) {
                    EnsureAllowed(path, sandboxRoot);
                    if (!string.IsNullOrWhiteSpace(sandboxRoot) && PathsEqual(path, sandboxRoot)) {
                        return FileOperationResult.Fail("Refusing to delete the sandbox root.");
                    }
                }

                if (recycle && string.IsNullOrWhiteSpace(sandboxRoot)) {
                    string error;
                    if (!TrySendToRecycleBin(sources, out error)) {
                        return FileOperationResult.Fail(error);
                    }
                } else {
                    foreach (string path in sources) {
                        if (Directory.Exists(path)) {
                            Directory.Delete(path, true);
                        } else if (File.Exists(path)) {
                            File.SetAttributes(path, FileAttributes.Normal);
                            File.Delete(path);
                        }
                    }
                }

                return FileOperationResult.Ok(sources);
            } catch (Exception ex) {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        public static FileOperationResult CreateFolder(string parentDirectory, string name = null, string sandboxRoot = null) {
            try {
                if (string.IsNullOrWhiteSpace(parentDirectory)) {
                    return FileOperationResult.Fail("Parent directory is required.");
                }

                string parent = Path.GetFullPath(parentDirectory);
                EnsureAllowed(parent, sandboxRoot);
                if (!Directory.Exists(parent)) {
                    return FileOperationResult.Fail("The parent folder does not exist.");
                }

                string folderName = string.IsNullOrWhiteSpace(name) ? LocalizationStrings.NewFolderName : name.Trim();
                if (folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                    return FileOperationResult.Fail("The folder name contains invalid characters.");
                }

                string destination = UniquePath(Path.Combine(parent, folderName));
                EnsureAllowed(destination, sandboxRoot);
                Directory.CreateDirectory(destination);
                return FileOperationResult.Ok(destination);
            } catch (Exception ex) {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        public static FileOperationResult CreateFile(string parentDirectory, string name = null, string sandboxRoot = null) {
            try {
                if (string.IsNullOrWhiteSpace(parentDirectory)) {
                    return FileOperationResult.Fail("Parent directory is required.");
                }

                string parent = Path.GetFullPath(parentDirectory);
                EnsureAllowed(parent, sandboxRoot);
                if (!Directory.Exists(parent)) {
                    return FileOperationResult.Fail("The parent folder does not exist.");
                }

                string fileName = string.IsNullOrWhiteSpace(name) ? LocalizationStrings.NewFileName : name.Trim();
                if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                    return FileOperationResult.Fail("The file name contains invalid characters.");
                }

                string destination = UniquePath(Path.Combine(parent, fileName));
                EnsureAllowed(destination, sandboxRoot);
                using (File.Create(destination)) {
                }

                return FileOperationResult.Ok(destination);
            } catch (Exception ex) {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        public static string UniquePath(string path) {
            if (!File.Exists(path) && !Directory.Exists(path)) {
                return path;
            }

            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string fileName = Path.GetFileName(path);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            for (int i = 1; i < 10000; i++) {
                string candidate = Path.Combine(directory, stem + " (" + i + ")" + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) {
                    return candidate;
                }
            }

            return Path.Combine(directory, stem + " (" + Guid.NewGuid().ToString("N") + ")" + extension);
        }

        public static void EnsureAllowed(string path, string sandboxRoot) {
            if (string.IsNullOrWhiteSpace(sandboxRoot)) {
                return;
            }

            if (string.IsNullOrWhiteSpace(path)) {
                throw new InvalidOperationException("Refusing to operate on an empty path.");
            }

            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(sandboxRoot);
            if (!IsSubpathOf(full, root)) {
                throw new InvalidOperationException("Refusing to operate outside the sandbox.");
            }
        }

        public static bool IsSubpathOf(string path, string root) {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) {
                return false;
            }

            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string prefix = fullRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static FileOperationResult Transfer(IReadOnlyList<string> sources, string destinationDirectory, FileTransferMode mode, string sandboxRoot) {
            try {
                if (string.IsNullOrWhiteSpace(destinationDirectory)) {
                    return FileOperationResult.Fail("Destination directory is required.");
                }

                IReadOnlyList<string> items = NormalizeExistingPaths(sources);
                if (items.Count == 0) {
                    return FileOperationResult.Fail("Nothing to transfer.");
                }

                string destinationRoot = Path.GetFullPath(destinationDirectory);
                EnsureAllowed(destinationRoot, sandboxRoot);
                Directory.CreateDirectory(destinationRoot);

                List<string> affected = new List<string>();
                foreach (string source in items) {
                    EnsureAllowed(source, sandboxRoot);
                    string name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    string destination = UniquePath(Path.Combine(destinationRoot, name));
                    EnsureAllowed(destination, sandboxRoot);

                    if (IsSubpathOf(destination, source)) {
                        return FileOperationResult.Fail("Cannot copy or move a folder into itself.");
                    }

                    if (mode == FileTransferMode.Move && PathsEqual(Path.GetDirectoryName(source), destinationRoot)) {
                        if (!PathsEqual(source, destination)) {
                            if (Directory.Exists(source)) {
                                Directory.Move(source, destination);
                            } else {
                                File.Move(source, destination);
                            }
                        }
                    } else if (mode == FileTransferMode.Move) {
                        MovePath(source, destination);
                    } else {
                        CopyPath(source, destination);
                    }

                    affected.Add(destination);
                }

                return FileOperationResult.Ok(affected);
            } catch (Exception ex) {
                return FileOperationResult.Fail(ex.Message);
            }
        }

        private static void CopyPath(string source, string destination) {
            if (Directory.Exists(source)) {
                CopyDirectory(source, destination);
                return;
            }

            File.Copy(source, destination);
        }

        private static void MovePath(string source, string destination) {
            if (Directory.Exists(source)) {
                try {
                    Directory.Move(source, destination);
                    return;
                } catch (IOException) {
                    CopyDirectory(source, destination);
                    Directory.Delete(source, true);
                    return;
                }
            }

            try {
                File.Move(source, destination);
            } catch (IOException) {
                File.Copy(source, destination);
                File.SetAttributes(source, FileAttributes.Normal);
                File.Delete(source);
            }
        }

        private static void CopyDirectory(string source, string destination) {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }

            foreach (string directory in Directory.GetDirectories(source)) {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }

        private static IReadOnlyList<string> NormalizeExistingPaths(IReadOnlyList<string> paths) {
            List<string> result = new List<string>();
            if (paths == null) {
                return result;
            }

            foreach (string path in paths) {
                if (string.IsNullOrWhiteSpace(path)) {
                    continue;
                }

                string full = Path.GetFullPath(path);
                if ((File.Exists(full) || Directory.Exists(full)) && !result.Any(existing => PathsEqual(existing, full))) {
                    result.Add(full);
                }
            }

            return result;
        }

        private static bool PathsEqual(string left, string right) {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrySendToRecycleBin(IReadOnlyList<string> paths, out string error) {
            error = string.Empty;
            var builder = new StringBuilder();
            foreach (string path in paths) {
                builder.Append(path);
                builder.Append('\0');
            }
            builder.Append('\0');

            var operation = new SHFILEOPSTRUCT {
                wFunc = FO_DELETE,
                pFrom = builder.ToString(),
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING
            };

            int result = SHFileOperation(ref operation);
            if (result != 0) {
                error = "Recycle Bin operation failed with code " + result + ".";
                return false;
            }

            return true;
        }

        private const int FO_DELETE = 3;
        private const short FOF_ALLOWUNDO = 0x0040;
        private const short FOF_NOCONFIRMATION = 0x0010;
        private const short FOF_WANTNUKEWARNING = 0x4000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT {
            public IntPtr hwnd;
            public int wFunc;
            public string pFrom;
            public string pTo;
            public short fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }
    }
}
