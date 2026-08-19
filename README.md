# WinDock

> 用来解决 Win11 开始菜单太小的问题，并添加了快捷方式的备注功能。

WinDock 是一个 Windows 桌面启动器（Dock），把桌面、开始菜单和任务栏里的快捷方式聚合到一个可自定义的大面板中，解决 Win11 开始菜单应用列表太小、难以快速找到应用的问题。同时为每个快捷方式提供**备注**功能，方便给应用标注用途、分组说明等信息。

[English](README.en.md)

---

## ✨ 功能特性

- **大面板展示**：聚合桌面 / 开始菜单 / 任务栏固定中的应用，磁贴式布局，一目了然
- **快捷方式备注**：右键图标即可添加 / 编辑 / 清除备注（最多 20 字），单行显示，内容过长时鼠标悬停自动横向滚动
- **窗口拖拽**：按住窗口空白区域即可拖动；双击顶部空白区域切换最大化 / 还原；位置自动记忆
- **外观设置**：
  - 窗口阴影（关闭可显著降低内存占用）
  - 窗口透明度（30%–100%）
  - 图标透明度（0%–100%）
  - 图标大小（24–128，所有 Tab 同步生效）
- **排序方式**：默认（手动拖动排序）/ 名称正序 / 名称逆序 / 安装时间正序 / 安装时间逆序；支持拖拽换位
- **高清图标**：提取 256×256 系统大图标，高分屏下依然清晰
- **内存优化**：图标列表虚拟化（只实例化可见磁贴）、图标按需缓存、默认 100% 完全不透明
- **智能去重**：同一应用同时存在于桌面 / 开始菜单 / 任务栏时只保留一份；自动过滤 "Uninstall / 卸载" 类快捷方式
- **自动打包**：GitHub Actions 一键构建 Inno Setup 安装向导，打 tag 自动发布 Release

## 🖥️ 环境要求

- Windows 10 / 11（64 位）
- 安装包为自包含版本，**无需**预先安装 .NET 运行时

## 📦 安装与使用

### 方式一：安装包

从 [Releases](../../releases) 下载 `WinDock-Setup-x.y.z.exe`，双击安装即可（每用户安装，无需管理员权限）。

### 方式二：源码运行

```bash
git clone <your-repo-url>
cd WinDock
dotnet run --project WinDock/WinDock/WinDock/WinDock.csproj
```

### 基本交互

| 操作 | 效果 |
|---|---|
| 双击图标 | 启动应用 |
| 右键图标 | 备注（添加 / 编辑 / 清除）、移到默认 / 更多 / 隐藏、删除 |
| 拖动图标 | 在"默认"排序模式下调整顺序 |
| 按住空白区域拖动 | 移动窗口 |
| 双击顶部空白 | 最大化 / 还原 |
| 设置页 | 外观、排序方式、添加文件 / 文件夹、刷新图标列表 |

## 🛠️ 构建与开发

```bash
# 调试构建
dotnet build WinDock/WinDock/WinDock/WinDock.csproj -c Debug

# 自包含发布（win-x64）
dotnet publish WinDock/WinDock/WinDock/WinDock.csproj -c Release -r win-x64 --self-contained true -o publish
```

### 项目结构

```
WinDock/
├── .github/workflows/build.yml   # GitHub Actions 自动打包
├── installer/WinDock.iss         # Inno Setup 安装脚本
└── WinDock/
    ├── WinDock.slnx
    └── WinDock/
        ├── MainWindow.xaml(.cs)          # 主窗口与交互
        ├── Controls/VirtualizingWrapPanel.cs  # 虚拟化换行面板
        ├── Controls/MarqueeTextBlock.xaml(.cs) # 跑马灯备注控件
        ├── Models/                        # DockItem / DockStore
        └── Services/                      # 发现 / 目录 / 存储服务
```

## 📊 数据存储

所有数据（图标列表、分组、备注、外观与排序设置）保存在：

```
%LOCALAPPDATA%\WinDock\dock-items.json
```

删除该文件即可重置应用（重新扫描系统快捷方式）。

## 🤖 自动打包

推送代码后，GitHub Actions 会自动：
1. 自包含发布（win-x64）
2. 用 Inno Setup 生成安装向导 `WinDock-Setup-x.y.z.exe`
3. 上传构建产物；打 `vX.Y.Z` 标签时自动发布到 Releases

```bash
git tag v1.0.0
git push origin v1.0.0
```

## 📝 说明

- 应用只读取并聚合系统快捷方式，**不会修改或删除**真实的快捷方式文件
- 安装包未做代码签名，首次运行可能触发 Windows SmartScreen 提示，点击"更多信息 → 仍要运行"即可

## 📄 License

MIT License（如未指定，请以仓库 LICENSE 文件为准）。
