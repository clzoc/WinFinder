# WinFinder - macOS风格 Windows 文件管理器

![应用截图](https://github.com/user-attachments/assets/31e3388e-8fe4-4e67-b50f-93fa765ca7d8)

## 概览
具有 macOS 设计风格的 Windows 文件管理器，支持双窗格与多主题切换，包含以下特性：
- 连续曲率圆角窗口与 Squircle 剪裁
- 高饱和度色彩方案、精美 SVG 图标与流畅动画
- 列表 / 网格双视图，滚动虚拟化友好
- 面包屑路径展开（悬浮子目录弹出）、双窗格并排或上下布局切换
- 主题模式按钮：跟随系统、强制明亮、强制暗色（激活态背景可视化）

## 主要功能
### 核心功能
- 列表与网格视图切换
- 双窗格布局，支持水平并排或垂直分屏切换
- 面包屑导航，点击箭头展开子目录弹出
- 路径栏点击快速跳转，底部信息栏显示磁盘容量与选中计数
- 自动 / 明亮 / 暗色 三种主题模式切换

### 技术亮点
- 异步缩略图与虚拟化滚动，适配大目录
- Squircle 窗口与控件剪裁，DropShadow 效果
- 高 DPI 支持，SVG 图标系统
- 自然排序（文件名中的数字按数值比较）

## 安装与构建
```bash
git clone https://github.com/yourrepo/WinFinder.git
cd WinFinder
msbuild WinFinder.sln
```
> 若使用 Visual Studio，推荐启用 x64 Debug/Release 方案编译。

## 使用说明
- 顶部工具栏：返回 / 前进、列表 / 网格视图切换、双窗格布局切换。
- 左侧边栏：常用目录与磁盘入口；底部三枚模式按钮切换主题（Auto 跟随系统）。

## 文档
- [架构概述](ARCHITECTURE.md)
- [开发指南](CONTRIBUTING.md)
- 图标系统参考（`/icon` 目录，使用 SVG，已在 XAML 中引用）

## 开发路线
- [ ] 自定义缩略图系统
- [ ] 区域选择增强与上下文菜单完善

---

# WinFinder - macOS-inspired File Manager for Windows

## Overview
A macOS-style file manager for Windows with dual panes and theme switching:
- Continuous-curvature squircle window and clipped controls
- Vibrant palette, SVG icon set, and smooth animations
- List and grid views with virtualization for large folders
- Breadcrumb popups for subfolders; dual-pane layout switch (side-by-side or stacked)
- Theme buttons: Auto (follow system), Light, Dark with highlighted active state

## Core Features
- List/Grid view toggle
- Dual-pane layout: horizontal split or vertical stack
- Breadcrumb navigation with subfolder popup on arrow click
- Path bar jumps, footer showing disk space and selection count
- Auto/Light/Dark theme modes

## Technical Highlights
- Async thumbnails and virtualized scrolling
- Squircle shapes, drop shadows, high-DPI support
- Natural sort (numbers compare by value)
- SVG icon pipeline

## Build
```bash
git clone https://github.com/yourrepo/WinFinder.git
cd WinFinder
msbuild WinFinder.sln
```
> Visual Studio users: build with the x64 Debug/Release configuration.

## Usage
- Top toolbar: back/forward, view toggle, dual-pane layout toggle.
- Left sidebar: favorite folders and drives; bottom three buttons toggle Auto/Light/Dark themes.
- Breadcrumb: click nodes to jump; click arrows to open subfolder popup.

## Roadmap
- [ ] Custom thumbnail pipeline
- [ ] Enhanced marquee selection and context menu
