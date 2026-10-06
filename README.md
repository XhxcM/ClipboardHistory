# 剪贴历史

Windows 11 x64，以及 .NET 10 官方仍支持的 Windows 10 x64 企业/LTSC 版本文本剪贴板历史工具。程序从首次运行后开始记录，只保留最近 72 小时内容；完全相同的文本仅显示最近使用的一条。

## 使用

- 默认快捷键：`Win+Alt+V`
- 窗口、任务栏、系统托盘和安装包使用统一的渐变玻璃图标
- 界面采用“静谧雾光”背景、独立卡片气泡和圆角长方形控件，并支持克制的悬停与按压动效
- 双击记录或按回车：复制回剪贴板并隐藏窗口
- `Esc`：隐藏窗口并继续在系统托盘运行
- 设置中可以关闭开机启动、修改快捷键（包括 `Win+Shift` 等双修饰键组合）、排除指定应用
- 本地数据：`%LocalAppData%\ClipboardHistory`

## 构建与验证

```powershell
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui .\artifacts\screenshots
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui-dark .\artifacts\screenshots\dark
dotnet publish .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\ClipboardHistory.iss
```

安装包输出到 `artifacts\installer`；截图模式只使用临时合成数据，输出到 `artifacts\screenshots`。
