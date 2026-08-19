# WinDock

> A launcher that solves the problem of the Win11 Start menu being too small, and adds a note feature for shortcuts.

WinDock is a Windows desktop dock that aggregates shortcuts from your Desktop, Start Menu, and taskbar pins into one customizable panel — solving the problem of the Win11 Start menu's cramped app list. Every shortcut also supports a **note**, so you can label its purpose, category, or any extra info.

[中文](README.md)

---

## ✨ Features

- **Big panel layout**: Aggregates apps from Desktop / Start Menu / Taskbar pins into a tile grid — everything at a glance
- **Shortcut notes**: Right-click any icon to add / edit / clear a note (max 20 chars); single-line display, auto horizontal marquee on hover when too long
- **Window dragging**: Drag any blank area to move the window; double-click the top blank area to toggle maximize / restore; position is remembered
- **Appearance settings**:
  - Window shadow (turning it off significantly reduces memory usage)
  - Window opacity (30%–100%)
  - Icon opacity (0%–100%)
  - Icon size (24–128, applied to all tabs)
- **Sorting**: Default (manual drag order) / Name A→Z / Name Z→A / Install time new→old / Install time old→new; drag to reorder
- **HD icons**: Extracts 256×256 system icons — crisp on high-DPI displays
- **Memory optimized**: Virtualized icon list (only visible tiles are instantiated), on-demand icon cache, fully opaque at 100% by default
- **Smart de-duplication**: One copy per app even if it exists on Desktop / Start Menu / Taskbar; "Uninstall / 卸载" shortcuts are filtered out automatically
- **Auto packaging**: GitHub Actions builds an Inno Setup installer; tagging a release publishes it automatically

## 🖥️ Requirements

- Windows 10 / 11 (64-bit)
- The installer is self-contained — **no** .NET runtime installation required

## 📦 Installation & Usage

### Option 1: Installer

Download `WinDock-Setup-x.y.z.exe` from [Releases](../../releases) and run it (per-user install, no admin rights needed).

### Option 2: Run from source

```bash
git clone <your-repo-url>
cd WinDock
dotnet run --project WinDock/WinDock/WinDock/WinDock.csproj
```

### Basic interactions

| Action | Result |
|---|---|
| Double-click an icon | Launch the app |
| Right-click an icon | Note (add / edit / clear), move to Default / More / Hidden, delete |
| Drag an icon | Reorder (in "Default" sort mode) |
| Drag blank area | Move the window |
| Double-click top blank area | Maximize / restore |
| Settings page | Appearance, sorting, add file / folder, refresh list |

## 🛠️ Build & Develop

```bash
# Debug build
dotnet build WinDock/WinDock/WinDock/WinDock.csproj -c Debug

# Self-contained publish (win-x64)
dotnet publish WinDock/WinDock/WinDock/WinDock.csproj -c Release -r win-x64 --self-contained true -o publish
```

### Project layout

```
WinDock/
├── .github/workflows/build.yml   # GitHub Actions auto packaging
├── installer/WinDock.iss         # Inno Setup installer script
└── WinDock/
    ├── WinDock.slnx
    └── WinDock/
        ├── MainWindow.xaml(.cs)          # Main window & interactions
        ├── Controls/VirtualizingWrapPanel.cs  # Virtualizing wrap panel
        ├── Controls/MarqueeTextBlock.xaml(.cs) # Marquee note control
        ├── Models/                        # DockItem / DockStore
        └── Services/                      # Discovery / catalog / storage
```

## 📊 Data storage

All data (icon list, groups, notes, appearance & sorting settings) is stored at:

```
%LOCALAPPDATA%\WinDock\dock-items.json
```

Delete this file to reset the app (shortcuts will be re-scanned).

## 🤖 Auto packaging

After pushing, GitHub Actions automatically:
1. Publishes a self-contained build (win-x64)
2. Builds the Inno Setup installer `WinDock-Setup-x.y.z.exe`
3. Uploads the artifact; pushing a `vX.Y.Z` tag publishes it to Releases

```bash
git tag v1.0.0
git push origin v1.0.0
```

## 📝 Notes

- The app only reads and aggregates existing shortcuts — it never modifies or deletes real shortcut files
- The installer is unsigned; Windows SmartScreen may warn on first run — click "More info → Run anyway"

## 📄 License

MIT License (if not specified, refer to the LICENSE file in the repository).
