# 验收记录

验收时间：2026-10-06（Asia/Shanghai）

## 已通过

- 环境：Windows 11 24H2，Build 26100.4351，x64。
- 工具链：.NET SDK 10.0.401；Inno Setup 6.7.3。
- Release 构建：成功，0 警告、0 错误；自包含发布成功；安装包编译成功且无警告。
- 自动自检：Release 版、自包含发布版和已安装版退出码均为 0；覆盖精确文本去重、最近使用置顶、大小写不敏感搜索、72 小时清理、历史持久化、排除应用设置持久化、双修饰键快捷键表示，以及按钮和历史气泡的运行时悬停动效。
- 真实剪贴板：连续写入 A、B、A 后，A 和 B 各 1 条，A 时间更新且排序在 B 之前；测试文本已从用户数据中精确移除。
- 单实例：重复启动会唤醒已隐藏的现有窗口，不创建第二个后台实例。
- 主窗口与设置页：使用临时合成数据分别生成 Fluent 浅色和深色截图，截图完成后临时数据和捕获进程均已清理；人工查看确认历史记录圆角气泡、背景、文字、输入框、按钮和强调色清晰，无重叠、遮挡或裁切。
- 默认快捷键：`Win+Alt+V` 注册成功，界面未报告快捷键冲突。
- 快捷键修改：修复前输入 `Win+Shift` 后仍显示 `Win+Alt+V`；修复后输入框显示并保存 `Win+Shift`。隐藏窗口后，旧的 `Win+Alt+V` 不再呼出，`Win+Shift` 可呼出；重启后仍然有效。
- 安装：最终安装包静默安装退出码 0；主程序、卸载器和 Windows 卸载登记存在；已安装主程序与发布版 SHA-256 一致。
- 用户设置保留：重新安装后仍为 `Win+Shift`；用户当前关闭了开机启动，因此当前用户 Run 项不存在，安装过程未擅自改回默认值。
- 卸载：退出码 0；程序目录、卸载登记和 Run 项均清除，用户历史数据目录保留。
- 最终状态：最终安装包已重新安装，程序正在运行，只有 1 个实例且主窗口可见。
- 隐私检查：项目源码、安装脚本和说明文档的敏感信息模式扫描结果为 0。

## 产物

- 安装包：`artifacts/installer/ClipboardHistory-Setup-1.0.0-x64.exe`
- 大小：51,692,755 字节
- SHA-256：`34EC4FEC86A8008015E458B86976655195A1845ECAE26D9AFAD1030B6F3BBEAE`
- 自包含主程序：`artifacts/publish/ClipboardHistory.exe`（172,991,557 字节）
- 自包含主程序 SHA-256：`05E416B8E56E96EB580E16941C46236835EBA9DD1E1F7AB497CF8FE3954207BD`
- 浅色主窗口：`artifacts/screenshots/main-window.png`（98,234 字节；SHA-256 `BD387095B93B5E13F1F3445F6343B5642E9E3D81D14ED4C52EA5E8FB03493ECA`）
- 浅色设置页：`artifacts/screenshots/settings-page.png`（70,771 字节；SHA-256 `54522E271DEA602C5D51E018CECDF91C6311ADB9946DFBD1122882BF92707BE4`）
- 深色主窗口：`artifacts/screenshots/dark/main-window.png`（98,312 字节；SHA-256 `4E98472C5C9027DF5E1F081B2EB0980138DB0C61E429ED5FCE2FB27485F8117C`）
- 深色设置页：`artifacts/screenshots/dark/settings-page.png`（70,382 字节；SHA-256 `4B5CFF1AA4FE7756C2BD8FC96EBA345BCBCCC69CD839E2C0F43E595AEEBFA537`）

## 尚未验证

- 当前仅有 Windows 11 24H2 环境；Windows 10 实机运行尚未验证。按 .NET 10 官方支持范围，Windows 10 仅承诺仍受支持的企业/LTSC 版本。
- Computer Use 未返回原生应用窗口，因此无法自动点击系统托盘或双击/回车执行“复制回剪贴板”；这些交互路径仍需人工验收。快捷键修改、注册、旧组合失效和重启持久化已用 Windows UI Automation 与原生按键输入验证。
- 浅色和深色已通过应用级主题覆盖生成并人工检查；尚未通过 Windows 设置实际切换系统主题，也未验证其他 DPI 缩放。
- 当前 Inno Setup 未附带简体中文语言文件，安装向导使用英文；程序界面与说明文档为中文。
