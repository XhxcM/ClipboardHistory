# 验收记录

验收时间：2026-10-09（Asia/Shanghai）

## 环境

- Windows 11 24H2，Build 26100，x64。
- 窗口 DPI：192（200%）。
- .NET SDK 10.0.401；Inno Setup 6.7.3。
- 逻辑处理器：14。

## UI 重设计性能基线（改造前）

被测程序为旧版已安装程序 `D:\application\ClipboardHistory\ClipboardHistory.exe`，SHA-256 `7C7EF876456C1D21AC500349C1DC05D5DDEBD83701DB8B0C152B6A6D012FDA85`。

- 起始验证：Release 构建成功，0 警告、0 错误；自检退出码 0。
- 计算方法：每个状态采样 60 秒；平均 CPU = `CPU TotalProcessorTime 增量 / 实际秒数 / 14 × 100%`；工作集为采样结束时 `Refresh()` 后的 `WorkingSet64`。
- 托盘隐藏：60.00 秒平均 CPU `0.0000%`，结束工作集 `309.09 MB`。
- 窗口可见：60.02 秒平均 CPU `0.0632%`，结束工作集 `336.98 MB`。
- 最小化：改造前未获得可靠值。
- 测量只读取进程路径、文件哈希、CPU 时间、工作集和窗口句柄，没有读取或输出历史记录与设置内容。

## Monolyth UI 验收（改造后）

### 自动检查

- `dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release`：成功，0 警告、0 错误。
- `dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test`：退出码 0。
- 已安装 `D:\application\ClipboardHistory\ClipboardHistory.exe --self-test`：退出码 0。
- 自检覆盖原有去重、搜索、72 小时清理、持久化与快捷键表示，以及批准浅/深色板、共享样式、WindowChrome、标题栏可访问名称、列表虚拟化、无逐项阴影/缩放、动效停止策略、4 FPS 上限、设置空状态、上下文移除和弹层背景隔离/安全焦点。

### 截图检查

- `--capture-ui` 固定浅色，`--capture-ui-dark` 固定深色；均关闭动效、使用固定时钟和临时合成数据。连续运行两轮，8 张截图的 SHA-256 逐项一致。
- 已按原始 1280×1400 像素逐张检查 8 张 PNG：无裁切、重叠、破损字形、不可读文字、线路穿过正文、强调色不一致、空白中间栏或浅/深层级回退。
- 首轮设置页底部输入行存在裁切，已缩短排除列表并减少间距；主搜索框增加用途提示。修复后重新生成并复查通过。

### Windows 11 原生交互

Computer Use 未暴露原生应用清单，因此使用 Windows UI Automation 与原生窗口命中/输入消息执行；脚本只输出布尔结果，不读取或打印历史文本。

以下 21 项均通过：

- 标题栏拖动命中、边缘缩放命中、实际拖动、实际调整尺寸。
- 双击标题栏最大化、右键系统菜单、Windows 贴靠。
- 最大化、还原、最小化和关闭到托盘按钮。
- 单实例重新显示与当前用户全局快捷键呼出。
- 搜索无结果状态、历史项选择、历史右键菜单。
- 打开设置、`Esc` 返回主界面。
- 清空确认弹层、取消为初始焦点、Tab 在两个按钮间循环、取消后恢复主界面。
- 实际修改 `AppsUseLightTheme` 后，窗口背景无需重启即切换到批准色板；随后恢复原注册表值，窗口颜色同步恢复。

### 性能

最终安装版使用 3 个信号点、16 秒周期，并将慢速 Storyboard 限制为 4 FPS：

| 状态 | 60 秒平均 CPU | 结束工作集 | 对比旧版 |
|---|---:|---:|---|
| 可见 | `0.0763%` | `239.37 MB` | CPU `+0.0131` 个百分点；工作集 `-97.61 MB` |
| 托盘隐藏 | `0.0037%` | `226.98 MB` | CPU `+0.0037` 个百分点；工作集 `-82.11 MB` |
| 最小化 | `0.0000%` | `278.94 MB` | 旧版无可靠对照；无持续动画负载 |

- 可见 CPU 增量低于 `+0.5` 个百分点预算，UI 工作集没有增长。
- 首轮持续动画测得可见 `1.1941%`，暂停后降至 `0.0037%`，确认根因在环境 Storyboard。
- 8 FPS 安装版测得 `0.5895%`，仅高于上限 `0.0263` 个百分点；最终降为 4 FPS 后通过。

### 发布、安装与用户状态

- 自包含发布成功；Inno Setup 编译成功且无警告。
- 最终静默安装退出码 0；已安装版自检退出码 0。
- 已安装主程序 SHA-256 与发布版一致：`18A64109BC082DFFEC3D4E77BDAE0B31DDEB88CEF566A3F9D7125C5B8001F518`。
- 安装前后用户文件哈希完全一致：
  - `history.json`：`482F2F2C46CBF0A4F0F85A618A11CCEE7106706B4ACE5B1648AF404F2B35F829`
  - `settings.json`：`048B28D30581F3CE7E70E17C2204F7A0E0840BF236CC735305E8B534D67BA401`
- 当前用户已关闭开机启动；重装前后 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ClipboardHistory` 均不存在。
- 验证过程中未输出用户历史或设置内容。

## 产物

- 安装包：`artifacts/installer/ClipboardHistory-Setup-1.0.0-x64.exe`
  - 大小：51,975,862 字节
  - SHA-256：`6FBD0BFF55C4A4248BDDCF10C8AD79A94DECE96209558FC31993DC8AF428E227`
- 自包含程序：`artifacts/publish/ClipboardHistory.exe`
  - 大小：173,356,101 字节
  - SHA-256：`18A64109BC082DFFEC3D4E77BDAE0B31DDEB88CEF566A3F9D7125C5B8001F518`
- 浅色主窗口：92,086 字节；SHA-256 `2690A0CDDACA7C2E94C9EFF6635FCF071D7AAD436B1AE5AEAE7F2EBE30617923`
- 浅色设置页：114,364 字节；SHA-256 `048B498BA2A557FC50C101B05DE352523F949D09A128BF884D781048BD836608`
- 浅色清空弹层：113,172 字节；SHA-256 `B9EC0E81CF910D2D30CA87977A8A69EF64A52061DCAB2A07438CBC92881709A2`
- 浅色快捷键弹层：121,297 字节；SHA-256 `7131D268B8499B8BE7BE83949A632A6ABB8B9FE5A49654504BE590CBA70765BE`
- 深色主窗口：92,149 字节；SHA-256 `CB04E7FA4B9B22BE92829932B22C58CD4A60061CC45E0D337A4172D3F90E79E2`
- 深色设置页：114,662 字节；SHA-256 `0BABE5EC46FB8BC615811A5DA021460BF3B41DFAC2FF63C449171400E40F91B8`
- 深色清空弹层：106,578 字节；SHA-256 `3C348AEC5EBE8CC6CA29E8CE800670FAE9D82106089EC1A8CE29788E57445EB7`
- 深色快捷键弹层：115,484 字节；SHA-256 `7D12CBADE4DCA3C501212DD94D198021AA83A4A1C778210245D0D0399698B6D0`

## 尚未验证

- 当前只有 Windows 11 24H2；Windows 10 企业/LTSC 实机运行尚未验证。
- 当前覆盖 200% DPI；100%、125%、150% 等其他缩放未验证。
- 为避免读取或改写用户当前剪贴板，本轮未在最终版本实际执行双击/回车复制。对应事件绑定与 `CopySelected()` 未改，旧版本的真实剪贴板验证曾通过。
- 未主动制造真实快捷键占用冲突；注册失败后的旧快捷键回滚代码未改，信息弹层已通过结构、自检和截图验证。
- 未在本轮重新执行卸载；安装器逻辑未修改。
- Inno Setup 未附带简体中文语言文件，安装向导使用英文；程序界面与说明文档为中文。
