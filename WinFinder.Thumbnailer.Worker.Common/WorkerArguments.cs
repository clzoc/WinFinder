using System;

namespace WinFinder.Thumbnailer.Worker.Common {
    public static class WorkerArguments {
        public static bool Has(string[] args, string name) {
            if (args == null) {
                return false;
            }

            foreach (string arg in args) {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        public static string GetOption(string[] args, string name, string fallback) {
            if (args == null) {
                return fallback;
            }

            for (int i = 0; i < args.Length - 1; i++) {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) {
                    return string.IsNullOrWhiteSpace(args[i + 1]) ? fallback : args[i + 1];
                }
            }

            return fallback;
        }

        public static string Quote(string value) {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
