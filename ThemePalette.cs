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
                WindowBackground = Brush("#F4F4F5"),
                TopBarBackground = Brush("#FAFAFA"),
                DividerBrush = Brush("#1418181B"),
                SidebarBackground = Brush("#ECECEE"),
                SidebarHover = Brush("#E4E4E7"),
                SidebarSelected = Brush("#DBEAFE"),
                SidebarText = Brush("#3F3F46"),
                SidebarSelectedText = Brush("#1E40AF"),
                PrimaryText = Brush("#18181B"),
                SecondaryText = Brush("#52525B"),
                MutedText = Brush("#71717A"),
                ControlBackground = Brush("#FFFFFF"),
                ControlHoverBackground = Brush("#F4F4F5"),
                ControlActiveBackground = Brush("#DBEAFE"),
                PaneBackground = Brush("#FFFFFF"),
                PaneInactiveBackground = Brush("#FFFFFF"),
                PaneActiveBackground = Brush("#FFFFFF"),
                PaneBorder = Brush("#E4E4E7"),
                PaneActiveBorder = Brush("#60A5FA"),
                ListAlternateBackground = Brush("#FAFAFA"),
                ListHoverBackground = Brush("#F4F4F5"),
                ListSelectedBackground = Brush("#DBEAFE"),
                ListSelectedText = Brush("#1E40AF"),
                BreadcrumbHoverBackground = Brush("#F4F4F5"),
                SelectionBoxBackground = Brush("#2E3B82F6"),
                SelectionBoxBorder = Brush("#3B82F6"),
                AccentShadow = Brush("#3B82F6"),
                AccentColor = (Color)ColorConverter.ConvertFromString("#3B82F6")
            };
        }

        public static ThemePalette CreateDark() {
            return new ThemePalette {
                WindowBackground = Brush("#121214"),
                TopBarBackground = Brush("#17171A"),
                DividerBrush = Brush("#24FFFFFF"),
                SidebarBackground = Brush("#1A1A1D"),
                SidebarHover = Brush("#26262B"),
                SidebarSelected = Brush("#23375A"),
                SidebarText = Brush("#D4D4D8"),
                SidebarSelectedText = Brush("#DBEAFE"),
                PrimaryText = Brush("#F4F4F5"),
                SecondaryText = Brush("#A1A1AA"),
                MutedText = Brush("#71717A"),
                ControlBackground = Brush("#1C1C20"),
                ControlHoverBackground = Brush("#26262B"),
                ControlActiveBackground = Brush("#2C3C5C"),
                PaneBackground = Brush("#161619"),
                PaneInactiveBackground = Brush("#161619"),
                PaneActiveBackground = Brush("#1A1A1E"),
                PaneBorder = Brush("#2A2A2F"),
                PaneActiveBorder = Brush("#60A5FA"),
                ListAlternateBackground = Brush("#19191C"),
                ListHoverBackground = Brush("#232327"),
                ListSelectedBackground = Brush("#23375A"),
                ListSelectedText = Brush("#DBEAFE"),
                BreadcrumbHoverBackground = Brush("#26262B"),
                SelectionBoxBackground = Brush("#3D3B82F6"),
                SelectionBoxBorder = Brush("#60A5FA"),
                AccentShadow = Brush("#60A5FA"),
                AccentColor = (Color)ColorConverter.ConvertFromString("#60A5FA")
            };
        }
    }
}
