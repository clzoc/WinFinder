using System;
using System.Globalization;

namespace WinFinder.Utilities {
    public static class LocalizationStrings {
        private static bool IsChinese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);

        public static string CommonPlacesTitle => IsChinese ? "常用目录" : "Common Places";
        public static string ColumnNameHeader => IsChinese ? "名称" : "Name";
        public static string ColumnDateHeader => IsChinese ? "日期" : "Date";
        public static string ColumnTypeHeader => IsChinese ? "类型" : "Type";
        public static string ColumnSizeHeader => IsChinese ? "大小" : "Size";

        public static string Desktop => IsChinese ? "桌面" : "Desktop";
        public static string Downloads => IsChinese ? "下载" : "Downloads";
        public static string Music => IsChinese ? "音乐" : "Music";
        public static string Pictures => IsChinese ? "图片" : "Pictures";
        public static string Videos => IsChinese ? "视频" : "Videos";
        public static string Documents => IsChinese ? "文稿" : "Documents";

        public static string FolderType => IsChinese ? "文件夹" : "Folder";

        public static string DriveLabel(string name) {
            return IsChinese ? $"盘({name})" : $"Drive ({name})";
        }

        public static string DriveUsage(string total, string free) {
            return IsChinese ? $"共 {total} 可用 {free} " : $"Total {total} Free {free} ";
        }

        public static string SearchResults(string query) {
            return IsChinese ? $"搜索：{query}" : $"Search: {query}";
        }

        public static string ItemsSummary(int total, int selected) {
            return IsChinese ? $"共 {total} 项 已选择 {selected} 项" : $"{total} items, {selected} selected";
        }

        public static string ScrollSelectionError => IsChinese ? "滚动到选中项错误" : "Failed to scroll to selection";
    }
}
