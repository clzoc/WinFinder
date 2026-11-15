using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using WinFinder.Utilities;

namespace WinFinder.Backend {
    public sealed class WorkspaceTabState {
        public string Path { get; set; }
        public string View { get; set; }
    }

    public sealed class PaneSessionState {
        public int ActiveTabIndex { get; set; }
        public List<WorkspaceTabState> Tabs { get; set; }
    }

    public sealed class WindowSessionState {
        public int Version { get; set; }
        public bool DualPane { get; set; }
        public bool Stacked { get; set; }
        public PaneSessionState Pane1 { get; set; }
        public PaneSessionState Pane2 { get; set; }
    }

    public static class SessionStore {
        public const int CurrentVersion = 1;

        public static string DefaultFilePath {
            get {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WinFinder",
                    "session.json");
            }
        }

        public static string GetDefaultStartPath() {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile)) {
                return userProfile;
            }

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents) && Directory.Exists(documents)) {
                return documents;
            }

            DriveInfo[] drives = DriveInfo.GetDrives();
            for (int i = 0; i < drives.Length; i++) {
                if (drives[i].IsReady && drives[i].DriveType == DriveType.Fixed) {
                    return drives[i].RootDirectory.FullName;
                }
            }

            string systemRoot = System.IO.Path.GetPathRoot(Environment.SystemDirectory);
            return string.IsNullOrWhiteSpace(systemRoot) ? @"C:\" : systemRoot;
        }


        public static string FormatTabTitle(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return LocalizationStrings.NewTabTitle;
            }

            string trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            string name = System.IO.Path.GetFileName(trimmed);
            if (!string.IsNullOrWhiteSpace(name)) {
                return name;
            }

            return path;
        }

        public static WindowSessionState CreateDefault(string defaultPath = null) {
            return Normalize(null, false, defaultPath);
        }

        public static void Save(WindowSessionState state, string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("A session file path is required.", nameof(filePath));
            }

            WindowSessionState normalized = Normalize(state, false, GetDefaultStartPath());
            string directory = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(normalized, Formatting.Indented);
            string temp = filePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(filePath)) {
                File.Replace(temp, filePath, null);
            } else {
                File.Move(temp, filePath);
            }
        }

        public static WindowSessionState Load(string filePath, string defaultPath = null) {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
                return CreateDefault(defaultPath);
            }

            try {
                string json = File.ReadAllText(filePath);
                WindowSessionState state = JsonConvert.DeserializeObject<WindowSessionState>(json);
                return Normalize(state, true, defaultPath);
            } catch (Exception) {
                return CreateDefault(defaultPath);
            }
        }

        public static WindowSessionState Normalize(WindowSessionState state, bool requireExistingDirectories, string defaultPath = null) {
            if (state == null) {
                state = new WindowSessionState();
            }

            if (state.Version <= 0) {
                state.Version = CurrentVersion;
            }

            string fallback = string.IsNullOrWhiteSpace(defaultPath) ? GetDefaultStartPath() : defaultPath;
            state.Pane1 = NormalizePane(state.Pane1, requireExistingDirectories, fallback);
            state.Pane2 = NormalizePane(state.Pane2, requireExistingDirectories, fallback);
            return state;
        }

        public static PaneSessionState NormalizePane(PaneSessionState pane, bool requireExistingDirectories, string defaultPath = null) {
            if (pane == null) {
                pane = new PaneSessionState();
            }

            string fallback = string.IsNullOrWhiteSpace(defaultPath) ? GetDefaultStartPath() : defaultPath;
            List<WorkspaceTabState> cleaned = new List<WorkspaceTabState>();
            if (pane.Tabs != null) {
                foreach (WorkspaceTabState tab in pane.Tabs) {
                    if (tab == null || string.IsNullOrWhiteSpace(tab.Path)) {
                        continue;
                    }

                    string path;
                    try {
                        path = System.IO.Path.GetFullPath(tab.Path);
                    } catch (Exception) {
                        continue;
                    }

                    if (requireExistingDirectories && !Directory.Exists(path)) {
                        continue;
                    }

                    string view = string.Equals(tab.View, "grid", StringComparison.OrdinalIgnoreCase) ? "grid" : "list";
                    cleaned.Add(new WorkspaceTabState {
                        Path = path,
                        View = view
                    });
                }
            }

            if (cleaned.Count == 0) {
                cleaned.Add(new WorkspaceTabState {
                    Path = fallback,
                    View = "list"
                });
            }

            pane.Tabs = cleaned;
            if (pane.ActiveTabIndex < 0) {
                pane.ActiveTabIndex = 0;
            }
            if (pane.ActiveTabIndex >= cleaned.Count) {
                pane.ActiveTabIndex = cleaned.Count - 1;
            }

            return pane;
        }
    }
}
