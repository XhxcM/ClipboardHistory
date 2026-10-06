# 剪贴历史项目上下文摘要

更新时间：2026-10-06（Asia/Shanghai）

## 一句话概况

这是一个面向 Windows 10/11 x64 的本地文本剪贴板历史程序，使用 .NET 10 WPF 实现。第一版功能、源码、自包含程序、安装包和自动验证均已交付；Goal 当前为 `blocked`，原因是缺少 Windows 10 实机和可控制原生窗口的验收环境。

## 用户与协作要求

- 用户是代码初学者，希望助手主动完成技术选择，不把实现细节反问给用户。
- 默认使用中文沟通。
- 采用最简、最少依赖、易维护的方案，只修改项目直接相关内容。
- 所有改动必须包含结果验证；不得泄露密码、密钥、令牌或真实剪贴板敏感内容。
- 遇到多种解释时必须说明差异；信息不足时停止猜测并请求必要证据。

## 原始目标

- 从程序首次运行后开始记录文本复制和剪切内容，只展示最近 72 小时。
- 完全相同的文本只保留一条；再次出现时更新时间、来源并置顶。
- 支持搜索、删除、清空、暂停记录，以及双击或回车复制回剪贴板。
- 默认开机启动并驻留系统托盘，使用 `Win+Alt+V` 呼出。
- 设置页可关闭开机启动、修改快捷键、排除指定应用。
- 仅保存到当前用户本机，不联网、不上传；来源只保存可执行文件名，不保存窗口标题。
- 单条文本超过 1 MB 时跳过；剪贴板占用和快捷键冲突不能导致崩溃。
- 第一版只支持文本，不做图片、文件、云同步、账户、收藏或多设备功能，也不记录真实粘贴动作或监听键盘内容。

## 当前状态

- Goal 状态：`blocked`，不是 `complete`。
- 功能实现、构建、发布和安装包已经完成。
- 当前实际安装位置：`D:\application\ClipboardHistory\ClipboardHistory.exe`。
- 当前运行实例：1 个，进程响应正常。
- 当前用户快捷键已设为 `Win+Shift`；全新设置的默认值仍为 `Win+Alt+V`。
- 用户当前关闭了开机启动，因此当前用户 Run 项不存在；重新安装保留了该设置。
- 这证明安装器已经允许用户把程序安装到自定义目录。

## 已实现功能

- Windows 原生剪贴板监听：`AddClipboardFormatListener` / `WM_CLIPBOARDUPDATE`。
- 精确文本去重：使用区分大小写的完整字符串比较。
- 重复内容再次复制时更新 `LastUsedAt`、来源程序并置顶。
- 72 小时自动清理；程序启动时和每 15 分钟执行一次。
- 搜索不区分大小写。
- 删除单条、清空全部、暂停/恢复记录。
- 双击、回车或右键菜单复制回剪贴板。
- 系统托盘菜单：打开、暂停/恢复、清空、退出。
- 默认全局快捷键 `Win+Alt+V`，可在设置中修改并检测冲突；支持 `Win+Shift` 等双修饰键组合。
- 默认当前用户开机启动，可关闭。
- 排除应用只保存 `.exe` 文件名。
- 单实例运行；再次启动会唤醒已有窗口。
- 浅色、深色和系统强调色使用 WPF Fluent 主题资源。
- 界面采用“静谧雾光”风格：明暗主题均有克制的双层渐变背景，历史记录使用独立全宽卡片，设置项使用分组卡片，按钮和输入框采用圆角长方形，并保留悬停与按压动效。
- 安装和运行均不要求管理员权限。

## 数据与隐私

- 数据目录：`%LocalAppData%\ClipboardHistory`。
- 历史文件：`history.json`。
- 设置文件：`settings.json`。
- 保存时先写 `.tmp`，再使用覆盖移动替换正式文件，降低中断损坏风险。
- 损坏的历史文件会改名为带时间戳的 `.corrupt-*` 备份。
- 程序不联网，不上传数据。
- UI 截图模式使用临时合成数据，不读取真实历史记录或真实剪贴板内容。

## 技术结构

| 文件 | 作用 |
|---|---|
| `src/ClipboardHistory/ClipboardHistory.csproj` | .NET 10 WPF、x64、WinExe 项目配置 |
| `App.xaml` / `App.xaml.cs` | 应用启动、自检、截图模式、单实例和进程间唤醒 |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | 主窗口、设置页、托盘、监听、快捷键和用户操作 |
| `HistoryStore.cs` | 去重、搜索、72 小时清理和历史持久化 |
| `AppSettings.cs` | 开机启动、暂停、快捷键和排除应用设置 |
| `NativeMethods.cs` | Windows 剪贴板、快捷键和前台进程名原生调用 |
| `StartupManager.cs` | 当前用户 `HKCU\...\Run` 开机启动项 |
| `SelfCheck.cs` | 无测试框架的最小自动自检，包含 UI 动效运行时检查 |
| `UiCapture.cs` | 使用临时合成数据生成浅色/深色截图 |
| `app.manifest` | `asInvoker`、PerMonitorV2 DPI 和 Windows 10/11 兼容声明 |
| `installer/ClipboardHistory.iss` | Inno Setup 当前用户安装包 |

## 构建与验证命令

```powershell
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui .\artifacts\screenshots
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui-dark .\artifacts\screenshots\dark
dotnet publish .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\ClipboardHistory.iss
```

## 已验证证据

- 验证系统：Windows 11 24H2，Build 26100.4351，x64。
- 工具链：.NET SDK 10.0.401；Inno Setup 6.7.3。
- Release 构建：0 警告、0 错误。
- Release 版和自包含发布版自检退出码均为 0。
- 自检覆盖：去重、更新时间与排序、搜索、72 小时清理、历史持久化、排除应用设置持久化、双修饰键快捷键表示，以及双层雾光背景、设置卡片、圆角字段、圆角长方形按钮和历史卡片动效。
- 真实剪贴板测试 A、B、A：A/B 各保留一条，A 更新时间并置顶；测试内容已从用户数据中精确移除。
- `Win+Shift` 修改链路已验证：输入框更新、设置保存、旧快捷键失效、新快捷键呼出及重启持久化均通过。
- 已验证静默安装、卸载、开机启动、单实例、隐藏启动与窗口唤醒。
- 已人工查看浅色和深色主窗口/设置页截图，无重叠、遮挡或裁切。
- 敏感信息模式扫描结果为 0。

## 最终产物

- 安装包：`artifacts/installer/ClipboardHistory-Setup-1.0.0-x64.exe`
- 大小：51,698,454 字节
- SHA-256：`1F091C60F2353DB9CB910B56543D26793724E98F97AEC7ECC304C55D68E6B2E4`
- 自包含程序：`artifacts/publish/ClipboardHistory.exe`
  - 大小：172,995,653 字节
  - SHA-256：`334BAEF157F434733B951264363E14F672128B3FD46D2D547ADB531A41486B46`
- 浅色截图：`artifacts/screenshots/main-window.png`、`settings-page.png`
- 深色截图：`artifacts/screenshots/dark/main-window.png`、`settings-page.png`
- 完整哈希和验收详情：`VERIFICATION.md`。

## 尚未完成与阻塞原因

- 没有 .NET 10 官方支持的 Windows 10 x64 企业/LTSC 实机或虚拟机，无法完成 Windows 10 实机验收。
- Computer Use 多次返回空应用清单，窗口枚举接口也不可用，无法代替用户完成托盘和双击/回车复制回剪贴板等人工点击验收。快捷键修改与触发已用 Windows UI Automation 和原生按键输入验证。
- 浅色/深色已通过应用级覆盖生成并检查，但尚未从 Windows 设置中实际切换系统主题。
- 其他 DPI 缩放尚未验证。
- Inno Setup 当前没有简体中文语言文件，因此安装向导为英文；程序界面和文档为中文。

恢复 Goal 的条件：提供可用的 Windows 10 环境，并恢复可枚举原生窗口的 Computer Use 通道，或由用户完成并反馈上述人工验收。

## 已撤回事项

用户曾反馈安装包下载和自定义位置问题，随后确认是操作误会，安装包没有问题且不需要修改。不要因此改动安装器。当前程序已安装到用户自选的 `D:\application\ClipboardHistory`。

## 如何打开程序

- 当前安装版按 `Win+Shift`；全新设置默认为 `Win+Alt+V`。
- 在开始菜单搜索“剪贴历史”。
- 双击系统托盘中的“剪贴历史”图标。
- 当前可直接运行：`D:\application\ClipboardHistory\ClipboardHistory.exe`。

## 后续继续工作的顺序

1. 先阅读 `GOAL.md`、本文件和 `VERIFICATION.md`。
2. 不要修改已经撤回的下载或安装位置事项。
3. 任何后续代码改动都要重建 Release、自包含程序和安装包，执行自检，并更新全部哈希。
4. 补齐 Windows 10 与人工交互验收后，才能把 Goal 从 `blocked` 改为 `complete`。

