using System.Windows.Media;

namespace WinFinder {
    public enum ThemeMode {
        Light,
        Dark
    }

    public class ThemePalette {
        public SolidColorBrush WindowBackground { get; set; }
        public SolidColorBrush TopBarBackground { get; set; }
        public SolidColorBrush DividerBrush { get; set; }
        public SolidColorBrush SidebarBackground { get; set; }
        public SolidColorBrush SidebarHover { get; set; }
        public SolidColorBrush SidebarSelected { get; set; }
        public SolidColorBrush SidebarText { get; set; }
        public SolidColorBrush SidebarSelectedText { get; set; }
        public SolidColorBrush PrimaryText { get; set; }
        public SolidColorBrush SecondaryText { get; set; }
        public SolidColorBrush MutedText { get; set; }
        public SolidColorBrush ControlBackground { get; set; }
        public SolidColorBrush ControlHoverBackground { get; set; }
        public SolidColorBrush ControlActiveBackground { get; set; }
        public SolidColorBrush PaneBackground { get; set; }
        public SolidColorBrush PaneInactiveBackground { get; set; }
        public SolidColorBrush PaneActiveBackground { get; set; }
        public SolidColorBrush PaneBorder { get; set; }
        public SolidColorBrush PaneActiveBorder { get; set; }
        public SolidColorBrush ListAlternateBackground { get; set; }
        public SolidColorBrush ListHoverBackground { get; set; }
        public SolidColorBrush ListSelectedBackground { get; set; }
        public SolidColorBrush ListSelectedText { get; set; }
        public SolidColorBrush BreadcrumbHoverBackground { get; set; }
        public SolidColorBrush SelectionBoxBackground { get; set; }
        public SolidColorBrush SelectionBoxBorder { get; set; }
        public SolidColorBrush AccentShadow { get; set; }
        public Color AccentColor { get; set; }
    }

    public static class ThemePalettes {
        private static SolidColorBrush Brush(string hex) {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public static ThemePalette CreateLight() {
            return new ThemePalette {
                WindowBackground = Brush("#F7F7F8"),
                TopBarBackground = Brush("#FCFCFD"),
                DividerBrush = Brush("#1A0F172A"),
                SidebarBackground = Brush("#F1F1F3"),
                SidebarHover = Brush("#E7E7EA"),
                SidebarSelected = Brush("#C9DEF8"),
                SidebarText = Brush("#303137"),
                SidebarSelectedText = Brush("#1F2025"),
                PrimaryText = Brush("#111827"),
                SecondaryText = Brush("#5F6068"),
                MutedText = Brush("#7C7E87"),
                ControlBackground = Brush("#FFFFFF"),
                ControlHoverBackground = Brush("#EEF1F5"),
                ControlActiveBackground = Brush("#E2EAF7"),
                PaneBackground = Brush("#FFFFFF"),
                PaneInactiveBackground = Brush("#FFFFFF"),
                PaneActiveBackground = Brush("#FFFFFF"),
                PaneBorder = Brush("#DCDCE2"),
                PaneActiveBorder = Brush("#4F7EEA"),
                ListAlternateBackground = Brush("#F7F7F8"),
                ListHoverBackground = Brush("#EFEFF2"),
                ListSelectedBackground = Brush("#C9DEF8"),
                ListSelectedText = Brush("#111827"),
                BreadcrumbHoverBackground = Brush("#ECECF0"),
                SelectionBoxBackground = Brush("#332563EB"),
                SelectionBoxBorder = Brush("#4F7EEA"),
                AccentShadow = Brush("#4F7EEA"),
                AccentColor = (Color)ColorConverter.ConvertFromString("#4F7EEA")
            };
        }

        public static ThemePalette CreateDark() {
            return new ThemePalette {
                WindowBackground = Brush("#0F0F10"),
                TopBarBackground = Brush("#111318"),
                DividerBrush = Brush("#26FFFFFF"),
                SidebarBackground = Brush("#171719"),
                SidebarHover = Brush("#25262A"),
                SidebarSelected = Brush("#2A2F3A"),
                SidebarText = Brush("#D7D7DE"),
                SidebarSelectedText = Brush("#F4F4F5"),
                PrimaryText = Brush("#ECEFF4"),
                SecondaryText = Brush("#B9BAC3"),
                MutedText = Brush("#8A8C96"),
                ControlBackground = Brush("#1A1D24"),
                ControlHoverBackground = Brush("#242832"),
                ControlActiveBackground = Brush("#2D3A50"),
                PaneBackground = Brush("#141416"),
                PaneInactiveBackground = Brush("#141416"),
                PaneActiveBackground = Brush("#18181B"),
                PaneBorder = Brush("#2A2B30"),
                PaneActiveBorder = Brush("#7AA2F7"),
                ListAlternateBackground = Brush("#17181B"),
                ListHoverBackground = Brush("#232428"),
                ListSelectedBackground = Brush("#2F6FEB"),
                ListSelectedText = Brush("#F8FAFC"),
                BreadcrumbHoverBackground = Brush("#25262A"),
                SelectionBoxBackground = Brush("#403B82F6"),
                SelectionBoxBorder = Brush("#7AA2F7"),
                AccentShadow = Brush("#7AA2F7"),
                AccentColor = (Color)ColorConverter.ConvertFromString("#7AA2F7")
            };
        }
    }
}
