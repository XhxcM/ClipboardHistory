# 剪贴历史项目交接摘要

更新日期：2026-10-09（Asia/Shanghai）

## 1. 当前状态

- 项目是面向 Windows 10/11 x64 的本地文本剪贴板历史工具，使用 .NET 10 WPF，无第三方运行时依赖。
- GitHub：`https://github.com/XhxcM/ClipboardHistory`；本次工作分支为 `feature/monolyth-ui`。
- 已安装至 `D:\application\ClipboardHistory\ClipboardHistory.exe`；已安装程序 SHA-256 与最终发布版一致。
- 当前用户快捷键为 `Win+Shift`；全新设置默认为 `Win+Alt+V`。
- 当前用户已关闭开机启动，最终重装后 Run 项仍不存在。
- 本次 Monolyth UI 重设计、性能调优、发布与安装已完成；整体 Goal 仍缺 Windows 10 实机及少数隐私敏感交互复验。

## 2. 已交付能力

- 记录最近 72 小时的文本复制/剪切；精确去重，重复内容更新时间、来源并置顶。
- 支持搜索、删除、清空、暂停/恢复，以及双击、回车或右键复制回剪贴板。
- 支持系统托盘、单实例、开机启动、可修改全局快捷键和排除指定应用。
- 数据仅保存在 `%LocalAppData%\ClipboardHistory`，不联网、不上传；单条文本上限 1 MB。
- UI 为原生低负载 Monolyth 风格：自定义标题栏、顶部透视线路、连续列表、分组设置页和窗口内弹层。
- 深浅色通过系统事件即时更新；3 个信号点以 16 秒周期、4 FPS 运行，并在隐藏、最小化、暂停、截图或系统关闭动画时停止。
- 窗口、EXE、任务栏、托盘和安装包继续使用同一套 16–256px 图标。

## 3. 关键文件

| 路径 | 用途 |
|---|---|
| `src/ClipboardHistory/App.xaml(.cs)` | 色板、共享样式、系统主题、启动、单实例与截图模式 |
| `src/ClipboardHistory/MainWindow.xaml(.cs)` | 自定义窗口、历史、设置、弹层、托盘、监听和快捷键 |
| `src/ClipboardHistory/HistoryStore.cs` / `AppSettings.cs` | 历史与设置持久化 |
| `src/ClipboardHistory/SelfCheck.cs` | 核心行为与 UI 契约自检 |
| `src/ClipboardHistory/UiCapture.cs` | 8 张确定性浅/深色静态截图 |
| `installer/ClipboardHistory.iss` | Inno Setup 当前用户安装包 |
| `GOAL.md` / `VERIFICATION.md` | 目标、证据、哈希和未验证项 |

## 4. 最终验证摘要

- 环境：Windows 11 24H2 Build 26100，x64，窗口 DPI 192（200%）；.NET SDK 10.0.401；Inno Setup 6.7.3。
- Release 构建 0 警告/0 错误；源码自检与已安装版自检退出码均为 0。
- 8 张浅/深色截图已按原图检查，无裁切、重叠、破损字形、正文线路穿透或层级回退。
- 21 项 UI Automation/原生窗口检查通过；实际系统应用主题切换和恢复通过。
- 性能：可见 `0.0763% / 239.37 MB`，隐藏 `0.0037% / 226.98 MB`，最小化 `0.0000% / 278.94 MB`。
- 重装前后 `history.json` 与 `settings.json` SHA-256 相同；开机启动仍关闭。

## 5. 当前产物

- 安装包：`artifacts/installer/ClipboardHistory-Setup-1.0.0-x64.exe`
  - 大小：51,975,862 字节
  - SHA-256：`6FBD0BFF55C4A4248BDDCF10C8AD79A94DECE96209558FC31993DC8AF428E227`
- 自包含程序：`artifacts/publish/ClipboardHistory.exe`
  - 大小：173,356,101 字节
  - SHA-256：`18A64109BC082DFFEC3D4E77BDAE0B31DDEB88CEF566A3F9D7125C5B8001F518`
- 截图：`artifacts/screenshots/` 与 `artifacts/screenshots/dark/`。
- 完整截图哈希与验收细节以 `VERIFICATION.md` 为唯一准据。

## 6. 未验证与继续规则

- Windows 10 企业/LTSC 实机未验证；当前只覆盖 Windows 11 24H2。
- 当前覆盖 200% DPI；其他 DPI 未验证。
- 本轮未执行会改写用户剪贴板的双击/回车复制，也未主动制造真实快捷键占用冲突；相关代码路径未改，UI 契约已验证。
- Computer Use 原生应用清单仍为空；原生交互由 Windows UI Automation 和窗口消息完成。
- 安装向导仍为英文；程序界面和文档为中文。
- 继续开发前先阅读本文件、`GOAL.md` 和 `VERIFICATION.md`；仅修改当前任务相关内容。
