# 剪贴历史项目交接摘要

更新日期：2026-10-06（Asia/Shanghai）

## 1. 当前状态

- 项目是面向 Windows 10/11 x64 的本地文本剪贴板历史工具，使用 .NET 10 WPF。
- GitHub：`https://github.com/XhxcM/ClipboardHistory`；当前主分支为 `main`。
- 已安装至 `D:\application\ClipboardHistory\ClipboardHistory.exe`，当前运行正常。
- 当前用户快捷键为 `Win+Shift`；全新设置默认为 `Win+Alt+V`。
- 当前用户已关闭开机启动，重新安装不得覆盖该设置。
- Goal 仍为 `blocked`：功能和产物已交付，仅缺 Windows 10 实机及部分原生交互验收。

## 2. 已交付能力

- 记录最近 72 小时的文本复制/剪切；精确去重，重复内容更新时间、来源并置顶。
- 支持搜索、删除、清空、暂停/恢复，以及双击、回车或右键复制回剪贴板。
- 支持系统托盘、单实例、开机启动、可修改全局快捷键和排除指定应用。
- 数据仅保存在 `%LocalAppData%\ClipboardHistory`，不联网、不上传；单条文本上限 1 MB。
- UI 为“静谧雾光”风格，支持深浅色、圆角卡片和克制动效；悬停卡片不再被视口裁切。
- 窗口、EXE、任务栏、托盘和安装包使用同一套 16–256px 渐变玻璃图标。

## 3. 关键文件

| 路径 | 用途 |
|---|---|
| `src/ClipboardHistory/App.xaml(.cs)` | 启动、主题、单实例、自检和截图模式 |
| `src/ClipboardHistory/MainWindow.xaml(.cs)` | 主界面、设置、托盘、监听和快捷键 |
| `HistoryStore.cs` / `AppSettings.cs` | 历史与设置持久化 |
| `SelfCheck.cs` | 无测试框架的最小自检 |
| `Assets/ClipboardHistory.png` / `.ico` | 图标主图与 Windows 多尺寸图标 |
| `installer/ClipboardHistory.iss` | Inno Setup 当前用户安装包 |
| `GOAL.md` / `VERIFICATION.md` | 完整目标、证据、哈希和未验收项 |

## 4. 构建与验证

```powershell
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
dotnet publish .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\ClipboardHistory.iss
```

- 已验证环境：Windows 11 24H2 x64；.NET SDK 10.0.401；Inno Setup 6.7.3。
- Release、自包含发布版和已安装版自检均通过，构建为 0 警告、0 错误。
- 任何代码改动都必须重建 Release、自包含程序和安装包，执行自检并更新 `VERIFICATION.md` 中的哈希。

## 5. 当前产物

- 安装包：`artifacts/installer/ClipboardHistory-Setup-1.0.0-x64.exe`
  - SHA-256：`59EE8159FFE3119ACB088D3AB7D8A02D98E9CBDB50C899E06DD9EEFC9AA9006E`
- 自包含程序：`artifacts/publish/ClipboardHistory.exe`
  - SHA-256：`7C7EF876456C1D21AC500349C1DC05D5DDEBD83701DB8B0C152B6A6D012FDA85`
- 截图：`artifacts/screenshots/`（浅色与 `dark/` 深色）。
- 完整产物大小、哈希和验收记录以 `VERIFICATION.md` 为唯一准据。

## 6. 未完成与继续规则

- 尚缺：Windows 10 企业/LTSC 实机验证；托盘、双击/回车复制等原生交互复验；Windows 设置中的真实主题切换及其他 DPI 缩放。
- 安装向导仍为英文；程序界面和文档为中文。
- 用户已撤回“安装包无法下载/自定义安装位置有问题”的反馈，不要因此改动安装器。
- 继续开发前先阅读本文件、`GOAL.md` 和 `VERIFICATION.md`；仅修改当前任务相关内容。
