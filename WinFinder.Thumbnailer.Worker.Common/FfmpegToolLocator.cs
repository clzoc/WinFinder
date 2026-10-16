using System;
using System.IO;

namespace WinFinder.Thumbnailer.Worker.Common {
    public static class FfmpegToolLocator {
        public static string FfmpegPath => ResolveFfmpegPath();
        public static bool IsAvailable => !string.IsNullOrWhiteSpace(FfmpegPath) && File.Exists(FfmpegPath);

        private static string ResolveFfmpegPath() {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] directCandidates = {
                Path.Combine(baseDirectory, "ffmpeg.exe"),
                Path.Combine(baseDirectory, "..", "..", "Tools", "ffmpeg", "ffmpeg.exe"),
                Path.Combine(baseDirectory, "..", "..", "..", "Tools", "ffmpeg", "ffmpeg.exe")
            };

            foreach (string candidate in directCandidates) {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath)) {
                    return fullPath;
                }
            }

            DirectoryInfo directory = new DirectoryInfo(baseDirectory);
            for (int i = 0; i < 8 && directory != null; i++, directory = directory.Parent) {
                string candidate = Path.Combine(directory.FullName, "Tools", "ffmpeg", "ffmpeg.exe");
                if (File.Exists(candidate)) {
                    return candidate;
                }
            }

            return string.Empty;
        }
    }
}
