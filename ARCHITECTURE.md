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