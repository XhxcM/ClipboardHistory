# 剪贴历史

面向 Windows 11 x64，以及 .NET 10 官方仍支持的 Windows 10 x64 企业/LTSC 版本的本地文本剪贴板历史工具。程序从首次运行后开始记录，只保留最近 72 小时内容；完全相同的文本仅显示最近使用的一条。

## 使用

- 默认快捷键：`Win+Alt+V`。
- 双击记录或按回车：复制回剪贴板并隐藏窗口。
- `Ctrl+F`：聚焦搜索；`Esc`：返回或隐藏窗口并继续在系统托盘运行。
- 设置中可以关闭开机启动、修改快捷键（包括 `Win+Shift` 等双修饰键组合）、排除指定应用。
- 本地数据：`%LocalAppData%\ClipboardHistory`；程序不联网、不上传。

## 界面

- 原生 WPF Monolyth 视觉：近黑背景、深灰黑表面、冷青强调色、顶部透视线路和圆角气泡历史项。
- 自定义标题栏保留拖动、缩放、最大化、贴靠、系统菜单和关闭到托盘。
- 固定使用近黑科技主题，不随 Windows 浅色模式切换；历史区和设置区保持透明，让电路背景完整贯穿窗口，设置页会适度降低线路亮度以保证文字清晰。
- 背景使用 13 条从窗口中部汇聚的透视电路线和 8 个青白发光节点，16 秒一轮并限制为 12 FPS；隐藏、最小化、暂停、截图或系统关闭动画时完全停止。
- 内容操作按钮恢复轻微悬停、上浮和按压反馈；历史气泡悬停时仅使用低开销变换，不使用逐项模糊阴影。
- 清空确认与快捷键冲突使用窗口内弹层，背景不可操作，支持循环 Tab、`Esc` 取消和焦点恢复。

## 构建与验证

```powershell
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui .\artifacts\screenshots
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui-dark .\artifacts\screenshots\dark
dotnet publish .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\ClipboardHistory.iss
```

安装包输出到 `artifacts\installer`。两个截图命令均固定使用近黑主题，只使用临时合成数据，生成主窗口、设置页、清空确认和快捷键冲突的静态截图。完整证据见 `VERIFICATION.md`。
