using System.IO;

namespace WinFinder.Thumbnailer.Worker.Common {
    public static class WorkerDiagnostics {
        public static string FirstLine(string fallback, params string[] values) {
            foreach (string value in values) {
                if (string.IsNullOrWhiteSpace(value)) {
                    continue;
                }

                using (var reader = new StringReader(value.Trim())) {
                    string line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line)) {
                        return line.Trim();
                    }
                }
            }

            return fallback;
        }
    }
}
