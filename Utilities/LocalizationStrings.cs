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

        public static string NewFolderName => IsChinese ? "新建文件夹" : "New Folder";
        public static string NewFileName => IsChinese ? "新建文件.txt" : "New File.txt";

        public static string MenuOpen => IsChinese ? "打开" : "Open";
        public static string MenuNewFolder => IsChinese ? "新建文件夹" : "New Folder";
        public static string MenuCut => IsChinese ? "剪切" : "Cut";
        public static string MenuCopy => IsChinese ? "复制" : "Copy";
        public static string MenuPaste => IsChinese ? "粘贴" : "Paste";
        public static string MenuRename => IsChinese ? "重命名" : "Rename";
        public static string MenuDelete => IsChinese ? "删除" : "Delete";
        public static string MenuCopyPath => IsChinese ? "复制路径" : "Copy Path";
        public static string MenuOpenTerminal => IsChinese ? "在终端中打开" : "Open in Terminal";
        public static string DialogOk => IsChinese ? "确定" : "OK";
        public static string DialogCancel => IsChinese ? "取消" : "Cancel";

        public static string ConfirmDeleteOne(string name) {
            return IsChinese ? $"确定要将“{name}”移到回收站吗？" : $"Move \"{name}\" to the Recycle Bin?";
        }

        public static string ConfirmDeleteMany(int count) {
            return IsChinese ? $"确定要将这 {count} 个项目移到回收站吗？" : $"Move these {count} items to the Recycle Bin?";
        }
    }
}
