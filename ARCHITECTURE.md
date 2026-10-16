# WinFinder 架构设计

## 核心模块

### 1. 界面系统
- Squircle窗口形状算法（Window_Corner）
- 双视图渲染引擎（ListView/GridView）
- SVG图标管理系统

### 2. 文件管理
- 虚拟化滚动系统
- 异步缩略图加载器
- 路径导航子系统

缩略图系统正在规划为独立本地服务，目标是摆脱 Windows Shell 缩略图、系统编解码器、K-Lite Codec Pack 和 HEIC 扩展的硬依赖。详细设计见 [缩略图架构](THUMBNAIL_ARCHITECTURE.md)。

### 3. 性能优化
- 内存管理池
- 滚动预测算法
- 缓存失效策略

## 关键技术
```csharp
// Squircle窗口形状生成算法示例
public string Window_Corner(double height, double width, double radius, double bias) {
    // 复杂几何运算生成连续曲率路径
    // 数学公式基于superellipse方程改进
    return pathString;
}
```

## 依赖关系
- Microsoft Windows API Code Pack
- SharpVectors SVG渲染库
- WPF虚拟化面板

## 缩略图系统方向
- 保留 `IThumbnailService` 作为 WinFinder 与缩略图能力的边界。
- 将重型解码能力迁移到独立 `WinFinder.Thumbnailer.Service` 进程。
- 使用本地 IPC、磁盘缓存、可取消的视口优先队列和格式解码插件。
- Windows Shell 缩略图只作为最后的 rich fallback，而不是主要生成路径。
