# 剪贴历史：压缩上下文

更新：2026-10-11（Asia/Shanghai）
用途：新会话先读本文件；需要完整验收证据、截图哈希或性能过程时再读 `VERIFICATION.md`，需要产品边界时读 `GOAL.md`。

## 当前快照

- 仓库：`D:\项目文件`，分支 `main`；Monolyth UI 已本地合并，尚未推送远端。
- 已安装：`D:\application\ClipboardHistory\ClipboardHistory.exe`；当前运行 1 个实例。
- 当前用户快捷键为 `Win+Shift`；新用户默认 `Win+Alt+V`。
- 当前用户已关闭开机启动；安装和重装必须保持该选择。
- UI 重设计、性能调优、发布、安装和 Windows 11 验收已完成。整体产品验收仍缺 Windows 10、其他 DPI 和少数隐私敏感交互。

## 产品契约

- .NET 10 WPF、Windows 10/11 x64、本地当前用户应用，无第三方运行时依赖。
- 记录最近 72 小时的文本复制/剪切；完全相同文本去重，重复时更新时间、来源并置顶。
- 支持搜索、删除、清空、暂停/恢复、托盘、单实例、全局快捷键、排除应用，以及双击/回车/右键复制。
- 单条文本上限 1 MB；数据位于 `%LocalAppData%\ClipboardHistory`，使用 JSON 持久化。
- 不联网、不上传；来源只保存可执行文件名，不保存窗口标题，不监听粘贴目标或键盘内容。
- 第一版只处理文本；不做图片、文件、云同步、账户、收藏或多设备功能。

## 已批准并实现的 UI 决策

- 目标是受 Unicorn/Monolyth 启发的高级桌面视觉，不复刻 WebGL。采用 WPF 原生 `WindowChrome`、矢量线路和资源色板，不引入 WebView2、视频、粒子、实时模糊或鼠标跟随。
- 深色：背景 `#0B1011`、表面 `#131A1C`、主文字 `#E8EEEE`、次要文字 `#929E9D`、强调色 `#45CDBB`。
- 浅色：背景 `#EEF2F2`、表面 `#F7F9F9`、主文字 `#111819`、次要文字 `#667372`、强调色 `#087D72`。
- 浅色次要文字放在表面色上，对比度约 `4.66:1`，满足普通文字 WCAG AA。
- 窗口为 `640×700`，使用自定义标题栏；拖动、缩放、双击最大化、系统菜单、贴靠和关闭到托盘均保留。
- 主界面使用顶部透视线路、连续虚拟化列表、固定底栏；设置页使用分组布局；清空和快捷键冲突使用可访问的窗口内弹层。
- 环境动效仅有 3 个信号点：16 秒一轮、4 FPS，只动画位移和透明度。窗口隐藏、最小化、暂停、截图或系统关闭客户端动画时停止。
- 深浅色通过 Windows 系统事件即时更新，不轮询；截图模式强制固定色板并关闭动画。

## 关键实现位置

| 文件 | 责任 |
|---|---|
| `src/ClipboardHistory/App.xaml(.cs)` | 色板、共享样式、系统主题、启动、单实例、截图入口 |
| `src/ClipboardHistory/MainWindow.xaml(.cs)` | 窗口外壳、历史、设置、弹层、托盘、监听、快捷键、动效生命周期 |
| `src/ClipboardHistory/HistoryStore.cs` | 72 小时清理、去重、搜索、JSON 历史 |
| `src/ClipboardHistory/AppSettings.cs` | 设置持久化 |
| `src/ClipboardHistory/SelfCheck.cs` | 数据行为和 UI 契约自检 |
| `src/ClipboardHistory/UiCapture.cs` | 固定时钟、固定主题的 8 张静态截图 |
| `installer/ClipboardHistory.iss` | Inno Setup 当前用户安装包 |

## 已验证事实

- 环境：Windows 11 24H2 Build 26100，x64，DPI 192（200%）；.NET SDK 10.0.401；Inno Setup 6.7.3。
- Release 构建为 0 警告、0 错误；源码、自包含版和已安装版自检均通过。
- 8 张浅/深色主窗口、设置页和两个弹层截图已原图检查；连续两轮截图 SHA-256 一致。
- 21 项 UI Automation/原生窗口检查通过，覆盖窗口行为、托盘、单实例、当前快捷键、搜索、右键菜单、设置、Esc 和弹层焦点循环。
- 实际修改并恢复 `AppsUseLightTheme` 后，窗口无需重启即可切换并恢复色板。
- 最终安装版 60 秒采样：可见 `0.1021% / 239.45 MB`，隐藏 `0.0019% / 245.54 MB`，最小化 `0.0446% / 286.20 MB`。
- 静默重装前后用户历史和设置文件哈希不变；Run 项继续不存在。

## 当前交付物

- 安装包：`artifacts/installer/ClipboardHistory-Setup-1.0.0-x64.exe`
  - 51,975,862 字节
  - SHA-256 `A61AC4D50A09F29ADC3B8AF623D39F2F4E04E627B11470236B838C4FAB22B512`
- 自包含程序：`artifacts/publish/ClipboardHistory.exe`
  - 173,356,101 字节
  - SHA-256 `6C5F54EFA7ECADBE35EAC42C16AD1FFA6593ACBE33FBAE315CC4DBFE8103E3FA`
- 已安装 EXE 与自包含程序哈希一致；截图位于 `artifacts/screenshots/` 和 `artifacts/screenshots/dark/`。
- `artifacts/` 被 Git 忽略。若从不同工作目录重新发布，单文件 EXE 或安装包哈希可能变化；以实际文件重新计算、更新 `VERIFICATION.md`，并确认已安装 EXE 与发布版一致。

## 最短验证命令

```powershell
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui .\artifacts\screenshots
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui-dark .\artifacts\screenshots\dark
dotnet publish .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\ClipboardHistory.iss
```

## 未验证与继续规则

- 未验证 Windows 10 企业/LTSC，以及 100%、125%、150% 等其他 DPI。
- 为避免改写用户剪贴板，最终版本未实际执行双击/回车复制；事件绑定和既有 `CopySelected()` 逻辑未改，旧版真实剪贴板验证曾通过。
- 未主动制造真实快捷键占用冲突；回滚逻辑未改，冲突弹层已通过自检和截图。
- 本轮未重新执行卸载；安装器逻辑未修改。安装向导仍为英文，程序和文档为中文。
- Computer Use 未暴露原生窗口；原生交互使用 Windows UI Automation 和窗口消息完成。
- 后续改动保持数据格式、用户设置和隐私边界；验证时只比较文件哈希，不输出历史、设置、密码、密钥或令牌内容。
- 发布或安装前先记录用户文件哈希和 Run 项状态；完成后再次比对。只有全部证据通过，才更新本文件和 `VERIFICATION.md`。
