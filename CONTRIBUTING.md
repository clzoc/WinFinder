# WinFinder 开发指南

## 开发环境要求
- Visual Studio 2022+ (需安装.NET Desktop开发组件)
- .NET Framework 4.7.2+
- Windows SDK 10.0.19041+
- 推荐扩展：
  - ReSharper Ultimate
  - WPF Toolkit
  - SVG Viewer

## 代码规范
1. 命名约定：
   - 类/接口：PascalCase
   - 方法：PascalCase
   - 私有字段：_camelCase
   - XAML控件：xxxControl后缀

2. XAML规范：
   ```xml
   <!-- 使用一致的元素排序 -->
   <Window>
     <DockPanel>
       <Menu DockPanel.Dock="Top"/>
       <StatusBar DockPanel.Dock="Bottom"/>
       <MainContent/>
     </DockPanel>
   </Window>
   ```

3. 异步模式：
   ```csharp
   public async Task LoadThumbnailsAsync()
   {
       await Task.Run(() => {
           // 耗时的缩略图生成逻辑
       });
   }
   ```

## 提交流程
1. 创建特性分支：
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. 提交信息格式：
   ```
   [类型] 简要描述

   - 详细说明变更内容
   - 关联的Issue编号(#123)
   ```

   类型选项：feat|fix|docs|style|refactor|test|chore

## 测试要求
- 新增功能需包含单元测试
- UI变更需更新ViewTests目录下的对应测试
- 性能关键代码需添加基准测试

## 文档规范
- 公共API必须包含XML注释
- 新增模块需更新ARCHITECTURE.md
- 图标资源变更需更新docs/ICONS.md

## 行为准则
- 遵守Semantic Versioning
- 重大变更需创建RFC文档
- 尊重社区贡献者代码风格