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
        public SolidColorBrush PaneBackground { get; set; }
        public SolidColorBrush PaneInactiveBackground { get; set; }
        public SolidColorBrush PaneActiveBackground { get; set; }
        public SolidColorBrush PaneBorder { get; set; }
        public SolidColorBrush PaneActiveBorder { get; set; }
        public SolidColorBrush ListAlternateBackground { get; set; }
        public SolidColorBrush ListHoverBackground { get; set; }
        public SolidColorBrush ListSelectedBackground { get; set; }
        public SolidColorBrush BreadcrumbHoverBackground { get; set; }
        public SolidColorBrush AccentShadow { get; set; }
        public Color AccentColor { get; set; }
    }

    public static class ThemePalettes {
        private static SolidColorBrush Brush(string hex) {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public static ThemePalette CreateLight() {
            return new ThemePalette {
                WindowBackground = Brush("#F3F3F3"),
                TopBarBackground = Brush("#F4F3F2"),
                DividerBrush = Brush("#22000000"),
                SidebarBackground = Brush("#EFEFEF"),
                SidebarHover = Brush("#DDE7F9"),
                SidebarSelected = Brush("#9ABAE8"),
                SidebarText = Brush("#111111"),
                SidebarSelectedText = Brush("#0D1A36"),
                PrimaryText = Brush("#111111"),
                SecondaryText = Brush("#303030"),
                MutedText = Brush("#606060"),
                PaneBackground = Brush("#FFFFFF"),
                PaneInactiveBackground = Brush("#FFFFFF"),
                PaneActiveBackground = Brush("#F3F7FF"),
                PaneBorder = Brush("#DDDDDD"),
                PaneActiveBorder = Brush("#2962D9"),
                ListAlternateBackground = Brush("#F5F5F5"),
                ListHoverBackground = Brush("#DDE7F9"),
                ListSelectedBackground = Brush("#2962D9"),
                BreadcrumbHoverBackground = Brush("#DDE7F9"),
                AccentShadow = Brush("#2962D9"),
                AccentColor = (Color)ColorConverter.ConvertFromString("#2962D9")
            };
        }

        public static ThemePalette CreateDark() {
            return new ThemePalette {
                WindowBackground = Brush("#1B1D21"),
                TopBarBackground = Brush("#22252B"),
                DividerBrush = Brush("#FF343843"),
                SidebarBackground = Brush("#1F2228"),
                SidebarHover = Brush("#2A3140"),
                SidebarSelected = Brush("#3E6BB5"),
                SidebarText = Brush("#E6E8EE"),
                SidebarSelectedText = Brush("#F6F7FB"),
                PrimaryText = Brush("#F1F3F6"),
                SecondaryText = Brush("#D5D8E0"),
                MutedText = Brush("#A0A6B3"),
                PaneBackground = Brush("#1F2228"),
                PaneInactiveBackground = Brush("#1F2228"),
                PaneActiveBackground = Brush("#252C37"),
                PaneBorder = Brush("#353841"),
                PaneActiveBorder = Brush("#5E8DF6"),
                ListAlternateBackground = Brush("#232832"),
                ListHoverBackground = Brush("#2F3746"),
                ListSelectedBackground = Brush("#4C7AE0"),
                BreadcrumbHoverBackground = Brush("#2F3746"),
                AccentShadow = Brush("#5E8DF6"),
                AccentColor = (Color)ColorConverter.ConvertFromString("#5E8DF6")
            };
        }
    }
}
