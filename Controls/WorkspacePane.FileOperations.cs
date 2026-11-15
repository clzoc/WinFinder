using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinFinder.Backend;
using WinFinder.Utilities;

namespace WinFinder.Controls {
    public partial class WorkspacePane {
        internal IReadOnlyList<string> GetSelectedPaths() {
            return ListInfo
                .Where(item => item.IsSelected)
                .Select(GetItemFullPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void WorkspacePane_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.OriginalSource is TextBox || e.OriginalSource is PasswordBox) {
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            if (alt) {
                return;
            }

            if (ctrl && e.Key == Key.A) {
                foreach (MyStruct item in ListInfo) {
                    item.IsSelected = true;
                }
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.C) {
                CopySelection();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.X) {
                CutSelection();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.V) {
                PasteIntoCurrentDirectory();
                e.Handled = true;
                return;
            }

            if (ctrl && shift && e.Key == Key.N) {
                CreateNewFolder();
                e.Handled = true;
                return;
            }

            if (ctrl && shift && e.Key == Key.C) {
                CopySelectedPaths();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete) {
                DeleteSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F2) {
                RenameSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter) {
                MyStruct selected = ListInfo.FirstOrDefault(item => item.IsSelected);
                if (selected != null) {
                    ActivateItem(selected);
                    e.Handled = true;
                }
            }
        }

        private void WorkspacePane_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            MyStruct item = FindItemFromSource(e.OriginalSource as DependencyObject);
            if (item != null && !item.IsSelected) {
                foreach (MyStruct other in ListInfo.Where(entry => entry.IsSelected)) {
                    other.IsSelected = false;
                }
                item.IsSelected = true;
            }

            ShowFileContextMenu();
            e.Handled = true;
        }

        private static MyStruct FindItemFromSource(DependencyObject source) {
            while (source != null) {
                if (source is FrameworkElement element && element.DataContext is MyStruct item) {
                    return item;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            return null;
        }

        private void ShowFileContextMenu() {
            IReadOnlyList<string> selected = GetSelectedPaths();
            bool hasSelection = selected.Count > 0;
            bool canPaste = !FileOperations.ClipboardState.IsEmpty;

            var menu = new ContextMenu();
            if (hasSelection) {
                menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuOpen, (s, e) => {
                    MyStruct item = ListInfo.FirstOrDefault(entry => entry.IsSelected);
                    ActivateItem(item);
                }));
            }

            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuNewFolder, (s, e) => CreateNewFolder()));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuCut, (s, e) => CutSelection(), hasSelection));
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuCopy, (s, e) => CopySelection(), hasSelection));
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuPaste, (s, e) => PasteIntoCurrentDirectory(), canPaste));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuRename, (s, e) => RenameSelection(), selected.Count == 1));
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuDelete, (s, e) => DeleteSelection(), hasSelection));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuCopyPath, (s, e) => CopySelectedPaths(), hasSelection));
            menu.Items.Add(CreateMenuItem(LocalizationStrings.MenuOpenTerminal, (s, e) => OpenTerminalHere()));

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.PlacementTarget = this;
            ContextMenu = menu;
            menu.IsOpen = true;
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler, bool enabled = true) {
            var item = new MenuItem {
                Header = header,
                IsEnabled = enabled
            };
            item.Click += handler;
            return item;
        }

        private void CopySelection() {
            IReadOnlyList<string> selected = GetSelectedPaths();
            if (selected.Count == 0) {
                return;
            }

            FileOperations.SetClipboard(selected, FileTransferMode.Copy);
        }

        private void CutSelection() {
            IReadOnlyList<string> selected = GetSelectedPaths();
            if (selected.Count == 0) {
                return;
            }

            FileOperations.SetClipboard(selected, FileTransferMode.Move);
        }

        private void PasteIntoCurrentDirectory() {
            ApplyResult(FileOperations.Paste(CurrentPath), true);
        }

        private void CreateNewFolder() {
            ApplyResult(FileOperations.CreateFolder(CurrentPath), true);
        }

        private void CopySelectedPaths() {
            IReadOnlyList<string> selected = GetSelectedPaths();
            if (selected.Count == 0) {
                return;
            }

            try {
                Clipboard.SetText(string.Join(Environment.NewLine, selected));
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "WinFinder", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteSelection() {
            IReadOnlyList<string> selected = GetSelectedPaths();
            if (selected.Count == 0) {
                return;
            }

            string message = selected.Count == 1
                ? LocalizationStrings.ConfirmDeleteOne(Path.GetFileName(selected[0]))
                : LocalizationStrings.ConfirmDeleteMany(selected.Count);
            MessageBoxResult confirm = MessageBox.Show(
                message,
                LocalizationStrings.MenuDelete,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) {
                return;
            }

            ApplyResult(FileOperations.Delete(selected, true), true);
        }

        private void RenameSelection() {
            IReadOnlyList<string> selected = GetSelectedPaths();
            if (selected.Count != 1) {
                return;
            }

            string path = selected[0];
            string currentName = Path.GetFileName(path);
            string newName = PromptForName(LocalizationStrings.MenuRename, currentName);
            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, currentName, StringComparison.Ordinal)) {
                return;
            }

            ApplyResult(FileOperations.Rename(path, newName), true);
        }

        private void OpenTerminalHere() {
            string directory = CurrentPath;
            IReadOnlyList<string> selected = GetSelectedPaths();
            if (selected.Count == 1 && Directory.Exists(selected[0])) {
                directory = selected[0];
            }

            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) {
                return;
            }

            try {
                var start = new ProcessStartInfo {
                    FileName = "wt.exe",
                    Arguments = "-d \"" + directory + "\"",
                    UseShellExecute = true
                };
                Process.Start(start);
            } catch {
                try {
                    Process.Start(new ProcessStartInfo {
                        FileName = "powershell.exe",
                        Arguments = "-NoExit -Command Set-Location -LiteralPath '" + directory.Replace("'", "''") + "'",
                        UseShellExecute = true
                    });
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "WinFinder", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ApplyResult(FileOperationResult result, bool refresh) {
            if (result == null) {
                return;
            }

            if (!result.Success) {
                MessageBox.Show(result.Error, "WinFinder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (refresh) {
                RefreshCurrentDirectoryFromBackend();
            }
        }

        private string PromptForName(string title, string initial) {
            var window = new Window {
                Title = title,
                Width = 360,
                Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = Window.GetWindow(this),
                Background = palette.WindowBackground
            };

            var textBox = new TextBox {
                Text = initial ?? string.Empty,
                Margin = new Thickness(16, 16, 16, 8)
            };
            var ok = new Button {
                Content = LocalizationStrings.DialogOk,
                Width = 80,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            var cancel = new Button {
                Content = LocalizationStrings.DialogCancel,
                Width = 80,
                Height = 28,
                IsCancel = true
            };

            string result = null;
            ok.Click += (s, e) => {
                result = textBox.Text;
                window.DialogResult = true;
                window.Close();
            };
            cancel.Click += (s, e) => window.Close();

            var buttons = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var layout = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Bottom);
            layout.Children.Add(buttons);
            layout.Children.Add(textBox);
            window.Content = layout;
            window.Loaded += (s, e) => {
                textBox.Focus();
                string extension = Path.GetExtension(textBox.Text);
                int selectLength = string.IsNullOrEmpty(extension)
                    ? textBox.Text.Length
                    : Math.Max(0, textBox.Text.Length - extension.Length);
                textBox.Select(0, selectLength);
            };

            bool? confirmed = window.ShowDialog();
            return confirmed == true ? result : null;
        }
    }
}
