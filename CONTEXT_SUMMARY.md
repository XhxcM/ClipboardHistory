# 剪贴历史：压缩上下文

更新：2026-10-11（Asia/Shanghai）
用途：新会话先读本文件；需要完整验收证据、截图哈希或性能过程时再读 `VERIFICATION.md`，需要产品边界时读 `GOAL.md`。

## 当前快照

- 仓库：`D:\项目文件`，分支 `test2`；动效与历史气泡回归修复尚未提交或推送。
- 已安装：`D:\application\ClipboardHistory\ClipboardHistory.exe`；当前运行 1 个实例。
- 当前用户快捷键为 `Win+Shift`；新用户默认 `Win+Alt+V`。
- 当前用户已关闭开机启动；安装和重装必须保持该选择。
- UI 重设计、动效回归修复、固定深色纠偏、发布、安装和 Windows 11 验收已完成。整体产品验收仍缺 Windows 10、其他 DPI 和少数隐私敏感交互。

## 产品契约

- .NET 10 WPF、Windows 10/11 x64、本地当前用户应用，无第三方运行时依赖。
- 记录最近 72 小时的文本复制/剪切；完全相同文本去重，重复时更新时间、来源并置顶。
- 支持搜索、删除、清空、暂停/恢复、托盘、单实例、全局快捷键、排除应用，以及双击/回车/右键复制。
- 单条文本上限 1 MB；数据位于 `%LocalAppData%\ClipboardHistory`，使用 JSON 持久化。
- 不联网、不上传；来源只保存可执行文件名，不保存窗口标题，不监听粘贴目标或键盘内容。
- 第一版只处理文本；不做图片、文件、云同步、账户、收藏或多设备功能。

## 已批准并实现的 UI 决策

- 目标是受 Unicorn/Monolyth 启发的高级桌面视觉，不复刻 WebGL。采用 WPF 原生 `WindowChrome`、矢量线路和资源色板，不引入 WebView2、视频、粒子、实时模糊或鼠标跟随。
- 深色：近黑背景 `#050708`、表面 `#0B1012`、主文字 `#E8EEEE`、次要文字 `#929E9D`、强调色 `#45CDBB`。
- 软件固定使用上述近黑色板，不再跟随 Windows 浅色模式。
- 窗口为 `640×700`，使用自定义标题栏；拖动、缩放、双击最大化、系统菜单、贴靠和关闭到托盘均保留。
- 主界面使用全窗口透视电路平面、透明历史容器、圆角气泡式虚拟化列表和固定底栏；设置页使用透明分组布局及 40 像素左右留白，并将动态背景亮度降至 `34%` 以保护文字可读性；清空和快捷键冲突使用可访问的窗口内弹层。
- 环境动效包含 13 条从窗口中部汇聚的线路、8 个青白多层发光节点和轻微线路呼吸：16 秒一轮、12 FPS，只动画位移和透明度；节点在循环复位前完全淡出。窗口隐藏、最小化、暂停、截图或系统关闭客户端动画时停止。
- 内容操作按钮恢复旧版悬停放大/上浮和按压回弹；历史气泡使用轻微悬停变换，但不恢复逐项模糊阴影。
- 运行和截图模式均固定近黑色板；截图模式关闭动画。

## 关键实现位置

| 文件 | 责任 |
|---|---|
| `src/ClipboardHistory/App.xaml(.cs)` | 固定深色色板、共享样式、启动、单实例、截图入口 |
| `src/ClipboardHistory/MainWindow.xaml(.cs)` | 窗口外壳、历史、设置、弹层、托盘、监听、快捷键、动效生命周期 |
| `src/ClipboardHistory/HistoryStore.cs` | 72 小时清理、去重、搜索、JSON 历史 |
| `src/ClipboardHistory/AppSettings.cs` | 设置持久化 |
| `src/ClipboardHistory/SelfCheck.cs` | 数据行为和 UI 契约自检 |
| `src/ClipboardHistory/UiCapture.cs` | 固定时钟、固定深色主题的静态截图 |
| `installer/ClipboardHistory.iss` | Inno Setup 当前用户安装包 |

## 已验证事实

- 环境：Windows 11 24H2 Build 26100，x64，DPI 192（200%）；.NET SDK 10.0.401；Inno Setup 6.7.3。
- Release 构建为 0 警告、0 错误；源码、自包含版和已安装版自检均通过。
- 主窗口、设置页和两个弹层的固定深色截图已原图检查；两个兼容截图入口生成的 4 张对应 PNG 的 SHA-256 逐项一致。
- 实际可见窗口下部安全区域帧差确认线路/信号区域在 2.25 秒内发生明显变化；按钮和历史气泡悬停变换由自检覆盖。
- 21 项 UI Automation/原生窗口检查通过，覆盖窗口行为、托盘、单实例、当前快捷键、搜索、右键菜单、设置、Esc 和弹层焦点循环。
- 主页面和设置页面均无整块内容底板，设置页左右留白为 40 像素；固定深色主题不受 Windows 浅色模式影响。
- 本次安装版稳定后 60 秒采样：单核口径 CPU `5.2603%`、按 14 个逻辑处理器归一化为整机 `0.3757%`，工作集 `355.30 MB`，进程持续响应；CPU 增量仍在批准预算内，工作集比旧基线高约 `18.32 MB`；隐藏和最小化停止策略自检继续通过，本次未重新采样这两种状态。
- 静默重装前后用户历史和设置文件哈希不变；Run 项继续不存在。

## 当前交付物

- 安装包：`artifacts/installer/ClipboardHistory-Setup-1.0.0-x64.exe`
  - 51,982,914 字节
  - SHA-256 `50FFFD0C12F00D6D0F97D0A345C542AC0256A7495689DAABBB5ABD92B2F0787E`
- 自包含程序：`artifacts/publish/ClipboardHistory.exe`
  - 173,368,389 字节
  - SHA-256 `2C0E5BD14C9071B224CD80D7F0DC839DE35FDB4639F6C7BA6A458C3B6D133F03`
- 已安装 EXE 与自包含程序哈希一致；本轮截图位于 `artifacts/screenshots/fixed-final/` 和 `artifacts/screenshots/fixed-final-dark/`。
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
