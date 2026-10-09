# Monolyth UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the ClipboardHistory WPF interface with the approved low-load Monolyth visual language while preserving all existing clipboard, settings, tray, shortcut, persistence, and installation behavior.

**Architecture:** Keep the existing single-window WPF structure and add no dependencies. Use `WindowChrome` for native window behavior, theme-aware WPF resources for the light/dark palette, cached static vector lines plus at most four transform/opacity signal animations, and one reusable in-window dialog overlay. Extend the existing assert-based self-check and screenshot harness instead of adding a test framework.

**Tech Stack:** .NET 10, WPF, Windows Forms `NotifyIcon`, XAML styles/storyboards, existing `SelfCheck`, PowerShell 7, Inno Setup 6.

**Spec:** `docs/superpowers/specs/2026-10-08-monolyth-ui-redesign-design.md`

## Global Constraints

- Target Windows 10/11 x64 with `net10.0-windows`; Windows 10 support remains unverified until a supported Enterprise/LTSC machine is available.
- Add no NuGet package, WebView2, video, WebGL, particle engine, real-time blur, mouse tracking, or third-party UI framework.
- Keep `Application.ThemeMode="System"`; forced light/dark capture modes must remain deterministic.
- Animate only transforms and opacity; at most four ambient signal elements may loop.
- Stop ambient motion while minimized, hidden, paused, in UI capture, or when `SystemParameters.ClientAreaAnimation` is false.
- Preserve history JSON format, `%LocalAppData%\ClipboardHistory`, 72-hour cleanup, deduplication, search, 1 MB limit, shortcut registration, startup setting, exclusions, tray behavior, and current user data.
- Reinstallation must not re-enable the current user's disabled startup setting.
- Keep the Windows Forms tray menu native.
- Do not print or expose history contents, settings contents, credentials, tokens, passwords, or other secrets during verification.
- Use the approved palette values and Segoe UI Variable; low-saturation red is reserved for destructive actions.

## Review Focus

- A live Windows light/dark theme change must update the custom palette without restart or polling; Task 2 pins deterministic palettes and Task 7 captures both modes.
- Custom chrome must retain drag, resize, double-click maximize/restore, system menu, snap, minimize, close-to-tray, and DPI-safe hit targets; Task 3 owns these checks.
- A visible in-window dialog must block background commands, keep focus inside the dialog, make `Esc` cancel, and never let `Enter` copy a history item; Task 6 owns these checks.
- Hidden, minimized, paused, capture, and reduced-animation states must stop the ambient storyboard; Task 5 tests the policy truth table and Task 7 measures idle behavior.
- Rebuild and reinstall must preserve the existing settings and history files byte-for-byte unless the running application legitimately updates timestamps; Task 7 records hashes before and after installation.

---

### Task 1: Record the Pre-change Baseline

**Files:**
- Modify: `VERIFICATION.md`

**Interfaces:**
- Consumes: current installed `D:\application\ClipboardHistory\ClipboardHistory.exe` and existing `--startup` / single-instance show behavior.
- Produces: a dated baseline section containing current build/self-check results, visible and hidden 60-second CPU averages, working set, and the exact measurement conditions used by Task 7.

- [ ] **Step 1: Verify the starting tree and current product**

Run:

```powershell
git status --short
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
```

Expected: clean tree before this task, build succeeds with 0 warnings and 0 errors, self-check prints `ClipboardHistory self-check: PASS`.

- [ ] **Step 2: Measure the installed build in repeatable states**

Use the same logical processor count and 60-second CPU-time delta calculation for each state. Record process path, file SHA-256, logical processor count, average CPU percentage, and working set. Use the installed executable's existing `--startup` argument for the hidden state and a second launch to request the visible state. If native minimization cannot be automated safely, record that state as unmeasured rather than inventing a value.

Expected: visible and hidden measurements are recorded without printing user history or settings.

- [ ] **Step 3: Add the baseline evidence to `VERIFICATION.md`**

Add a `## UI 重设计性能基线（改造前）` section with the exact commands, measurements, and any unmeasured state.

- [ ] **Step 4: Review and commit the baseline**

Run:

```powershell
git diff --check
git diff -- VERIFICATION.md
git add -- VERIFICATION.md
git commit -m "test: record UI performance baseline"
```

Expected: the commit contains only the baseline evidence.

### Task 2: Add Theme-aware Monolyth Tokens and Shared Styles

**Files:**
- Modify: `src/ClipboardHistory/App.xaml`
- Modify: `src/ClipboardHistory/App.xaml.cs`
- Modify: `src/ClipboardHistory/SelfCheck.cs`

**Interfaces:**
- Consumes: WPF `ThemeMode`, existing Fluent dynamic resources, and Windows `UserPreferenceChanged` notification.
- Produces: `internal static void ApplyMonolythPalette(ResourceDictionary resources, bool dark)`, semantic resource keys, `ActionButtonStyle`, `DangerButtonStyle`, `CaptionButtonStyle`, `TextFieldStyle`, `ToggleStyle`, and styled menu resources used by later tasks.

- [ ] **Step 1: Add failing palette and style assertions**

Replace `VerifyMotionStyles` with `VerifyUiContract` and add `VerifyPaletteResources`. Assert that calling `App.ApplyMonolythPalette` on a temporary dictionary produces the approved exact colors for light and dark modes, and that the shared styles exist. Assert that `ActionButtonStyle` has no scale transform or perpetual storyboard.

```csharp
App.ApplyMonolythPalette(resources, dark: true);
Ensure(((SolidColorBrush)resources["MonolythBackgroundBrush"]).Color == (Color)ColorConverter.ConvertFromString("#0B1011"), "深色背景必须使用批准色值");
Ensure(((SolidColorBrush)resources["MonolythAccentBrush"]).Color == (Color)ColorConverter.ConvertFromString("#45CDBB"), "深色强调色必须使用批准色值");
```

- [ ] **Step 2: Run the self-check and confirm the contract fails**

Run:

```powershell
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
```

Expected: FAIL because `ApplyMonolythPalette` and the new resources do not exist.

- [ ] **Step 3: Implement palette application and event-driven system-theme updates**

Add `ApplyMonolythPalette(ResourceDictionary resources, bool dark)` to `App.xaml.cs`. On normal startup, read `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`, apply the matching palette once, and subscribe to `Microsoft.Win32.SystemEvents.UserPreferenceChanged`; dispatch palette replacement to the WPF thread and unsubscribe in `OnExit`. Missing or unreadable registry data must fall back to light without crashing. Do not poll.

- [ ] **Step 4: Add the new control styles beside temporary compatibility resources**

In `App.xaml`, add the semantic brushes and the six shared styles named in the Interfaces block. Use fixed corner rules: 10px fields and ordinary buttons, 0px list rows, 6px menus, and caption buttons shaped by the title bar rather than pills. Keep `TopGlowBrush`, `BottomGlowBrush`, `BubbleButtonStyle`, and `RoundedFieldStyle` temporarily because the old `MainWindow.xaml` still consumes them; Task 3 removes them in the same commit that migrates every caller.

- [ ] **Step 5: Run focused and full checks**

Run:

```powershell
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
```

Expected: 0 warnings, 0 errors, and PASS.

- [ ] **Step 6: Commit the theme foundation**

```powershell
git add -- src/ClipboardHistory/App.xaml src/ClipboardHistory/App.xaml.cs src/ClipboardHistory/SelfCheck.cs
git commit -m "feat: add Monolyth theme resources"
```

### Task 3: Build the Custom Window Shell and Main History View

**Files:**
- Modify: `src/ClipboardHistory/App.xaml`
- Modify: `src/ClipboardHistory/MainWindow.xaml:1-174`
- Modify: `src/ClipboardHistory/MainWindow.xaml.cs:198-297`
- Modify: `src/ClipboardHistory/SelfCheck.cs`

**Interfaces:**
- Consumes: Task 2 semantic brushes and styles.
- Produces: named elements `TitleBar`, `MinimizeButton`, `MaximizeButton`, `CloseButton`, `CircuitField`, `SignalLayer`, `MainContent`, `SearchBox`, `HistoryList`, `EmptyStateTitle`, `EmptyStateText`, `PauseButton`, and `ClearButton`; handlers `MinimizeWindow_Click`, `ToggleMaximizeWindow_Click`, `CloseWindow_Click`, and `Window_StateChanged`.

- [ ] **Step 1: Add failing shell and history assertions**

Extend `VerifyUiContract` to assert:

- `WindowChrome.GetWindowChrome(mainWindow)` is non-null with a positive caption height and resize border.
- `WindowStyle` is `None`, `AllowsTransparency` is false, default size is `640 × 700`, and minimum dimensions do not clip the footer.
- Caption buttons exist, use `CaptionButtonStyle`, and have automation names.
- `CircuitField` and `SignalLayer` exist without `Effect`.
- `HistoryList` is virtualizing and its item template has no `DropShadowEffect`, `ScaleTransform`, or hover lift.
- Search is represented by `SearchBox` without a separate visible “搜索” label.

- [ ] **Step 2: Run the self-check and confirm failure**

Run the Release self-check. Expected: FAIL on the first new shell assertion.

- [ ] **Step 3: Replace the window shell and top section**

Add the `System.Windows.Shell` namespace, set `WindowStyle="None"`, attach `WindowChrome` with a 44px caption and system resize border, keep `AllowsTransparency="False"`, set `Width="640" Height="700"`, and build the integrated title bar. Mark interactive caption elements with `WindowChrome.IsHitTestVisibleInChrome="True"`; allow the remaining caption area to retain native drag, double-click, right-click system menu, snap, and resize behavior.

- [ ] **Step 4: Implement caption commands without changing close semantics**

Implement the four handlers in the Interfaces block with `SystemCommands.MinimizeWindow`, `SystemCommands.MaximizeWindow`, `SystemCommands.RestoreWindow`, and `Close()`. `Close()` must continue through `Window_Closing`, so ordinary close hides to tray. `Window_StateChanged` updates the maximize button's accessible label and glyph.

- [ ] **Step 5: Replace the fog header and card list**

Create the static circuit field only in the title/search and unused-background areas. Rebuild the list as flat rows with a subtle divider, a hover surface, a selected left accent, two-line text, source left, and time right. Keep all existing event handlers and context commands. Preserve selection restoration in `RefreshHistory`.

After every XAML caller uses the new semantic styles, remove the temporary `TopGlowBrush`, `BottomGlowBrush`, `BubbleButtonStyle`, and `RoundedFieldStyle` resources from `App.xaml` in this same step.

- [ ] **Step 6: Distinguish empty history from no search results**

Update `RefreshHistory()` so `EmptyStateTitle` and `EmptyStateText` show the approved empty copy when `_history.Items.Count == 0`, and a separate “没有匹配内容” state when history exists but the filtered result is empty. No new persistence or search behavior is allowed.

- [ ] **Step 7: Rebalance the footer and paused state**

Place `PauseButton` left and `ClearButton` right. Update `UpdatePauseUi()` to set text and a named paused visual state without changing settings persistence or the native tray label.

- [ ] **Step 8: Verify and commit**

Run build and self-check; expected 0 warnings, 0 errors, PASS. Then:

```powershell
git add -- src/ClipboardHistory/App.xaml src/ClipboardHistory/MainWindow.xaml src/ClipboardHistory/MainWindow.xaml.cs src/ClipboardHistory/SelfCheck.cs
git commit -m "feat: redesign the main window shell"
```

### Task 4: Add the Low-load Ambient Motion Policy

**Files:**
- Modify: `src/ClipboardHistory/MainWindow.xaml`
- Modify: `src/ClipboardHistory/MainWindow.xaml.cs:39-96,198-280,335-369`
- Modify: `src/ClipboardHistory/SelfCheck.cs`

**Interfaces:**
- Consumes: Task 3 `SignalLayer`, window visibility/state events, and paused state.
- Produces: constructor `MainWindow(string? dataDirectory = null, bool manageStartup = true, bool animationsEnabled = true)`, `internal static bool ShouldRunAmbientMotion(bool animationsEnabled, bool clientAreaAnimation, bool isVisible, WindowState state, bool isPaused)`, `private void UpdateAmbientMotion()`, and `internal bool IsAmbientMotionRunning { get; private set; }`.

- [ ] **Step 1: Add the failing policy truth-table test**

Add assertions for the static policy:

```csharp
Ensure(MainWindow.ShouldRunAmbientMotion(true, true, true, WindowState.Normal, false), "可见活动窗口应允许运行动效");
Ensure(!MainWindow.ShouldRunAmbientMotion(false, true, true, WindowState.Normal, false), "截图模式必须关闭动效");
Ensure(!MainWindow.ShouldRunAmbientMotion(true, false, true, WindowState.Normal, false), "系统关闭动画时必须静态显示");
Ensure(!MainWindow.ShouldRunAmbientMotion(true, true, false, WindowState.Normal, false), "隐藏窗口必须停止动效");
Ensure(!MainWindow.ShouldRunAmbientMotion(true, true, true, WindowState.Minimized, false), "最小化窗口必须停止动效");
Ensure(!MainWindow.ShouldRunAmbientMotion(true, true, true, WindowState.Normal, true), "暂停记录时必须停止动效");
```

- [ ] **Step 2: Run the self-check and confirm failure**

Expected: FAIL because `ShouldRunAmbientMotion` does not exist.

- [ ] **Step 3: Add one controllable ambient storyboard**

Define one named storyboard for three or four signal elements. Its full cycle must be between 14 and 18 seconds, repeat indefinitely only while allowed, and animate only transforms and opacity. Do not attach storyboards to every history row.

- [ ] **Step 4: Implement lifecycle control**

Store the constructor flag, call `UpdateAmbientMotion()` from `Loaded`, `IsVisibleChanged`, `Window_StateChanged`, `UpdatePauseUi()`, and before exit. Use `SystemParameters.ClientAreaAnimation` and the pure policy method. `UpdateAmbientMotion()` begins or resumes the single storyboard when allowed and stops it otherwise; keep `IsAmbientMotionRunning` synchronized for self-check and diagnostics.

- [ ] **Step 5: Verify no expensive visual effects remain**

Extend `VerifyUiContract` to count at most four signal elements and assert no `DropShadowEffect`, `BlurEffect`, or animated layout property exists in the main history template.

- [ ] **Step 6: Verify and commit**

Run build and self-check; expected PASS. Then commit:

```powershell
git add -- src/ClipboardHistory/MainWindow.xaml src/ClipboardHistory/MainWindow.xaml.cs src/ClipboardHistory/SelfCheck.cs
git commit -m "feat: add low-load ambient motion"
```

### Task 5: Redesign Settings Without Adding Product Behavior

**Files:**
- Modify: `src/ClipboardHistory/MainWindow.xaml:176-246`
- Modify: `src/ClipboardHistory/MainWindow.xaml.cs:314-331,415-526`
- Modify: `src/ClipboardHistory/SelfCheck.cs`

**Interfaces:**
- Consumes: Task 2 styles and existing settings methods.
- Produces: named elements `SettingsPanel`, `ExcludedAppsEmptyState`, and the existing settings controls in the approved grouped layout; existing handlers keep their names.

- [ ] **Step 1: Add failing settings-layout assertions**

Assert that `StartupSettingsCard` and `ExcludedAppsCard` no longer exist, the approved section titles and empty state exist, the save action stays outside the scrolling region, and the shortcut field retains its automation name and `PreviewKeyDown` handler.

- [ ] **Step 2: Run self-check and confirm failure**

Expected: FAIL because the old card names still exist.

- [ ] **Step 3: Replace nested cards with grouped rows**

Rebuild the settings panel using spacing and headings. Keep `StartWithWindowsCheckBox`, `HotKeyTextBox`, `ExcludedAppsListBox`, `ExcludedAppTextBox`, and the existing save/add/remove logic. Show `ExcludedAppsEmptyState` when `_pendingExcludedApps.Count == 0`.

- [ ] **Step 4: Make removal contextual**

Move the remove action into the selected excluded-app row or make the single remove action visible only when an item is selected. Do not add per-item state or change the stored string list. Update `RefreshExcludedApps()` to maintain the empty and remove states.

- [ ] **Step 5: Add one-shot page transitions**

Use a single 160ms opacity/translate transition when opening or closing settings only if `SystemParameters.ClientAreaAnimation` and the constructor animation flag allow it. Otherwise switch immediately. No looping animation is allowed.

- [ ] **Step 6: Verify and commit**

Run build and self-check; expected PASS. Then commit:

```powershell
git add -- src/ClipboardHistory/MainWindow.xaml src/ClipboardHistory/MainWindow.xaml.cs src/ClipboardHistory/SelfCheck.cs
git commit -m "feat: simplify the settings layout"
```

### Task 6: Replace Message Boxes With an Accessible In-window Dialog

**Files:**
- Modify: `src/ClipboardHistory/MainWindow.xaml`
- Modify: `src/ClipboardHistory/MainWindow.xaml.cs:282-297,344-369,498-526`
- Modify: `src/ClipboardHistory/SelfCheck.cs`

**Interfaces:**
- Consumes: Task 2 styles and Task 3 `MainContent`.
- Produces: named elements `DialogOverlay`, `DialogTitleText`, `DialogMessageText`, `DialogCancelButton`, and `DialogPrimaryButton`; methods `private void ShowOverlayDialog(string title, string message, string primaryText, Action? primaryAction, bool showCancel, bool destructive, IInputElement? returnFocus)`, `private void CloseOverlayDialog()`, `DialogPrimaryButton_Click`, `DialogCancelButton_Click`, `internal void ShowClearDialogForPreview()`, `internal void ShowHotKeyConflictForPreview()`, and `internal void CloseDialogForPreview()`.

- [ ] **Step 1: Add failing dialog structure and behavior assertions**

Assert the overlay begins collapsed, uses `KeyboardNavigation.TabNavigation="Cycle"`, has no blur effect, and the cancel button is the initial safe focus target. Add a preview-contract check: show clear preview, assert overlay visible and `MainContent.IsEnabled == false`; close preview, assert overlay collapsed and content enabled.

- [ ] **Step 2: Run self-check and confirm failure**

Expected: FAIL because the dialog elements and preview methods do not exist.

- [ ] **Step 3: Implement the shared overlay and focus restoration**

Add the overlay above all page content but below native window ownership. `ShowOverlayDialog` stores the prior focus and deferred action, disables `MainContent`, configures semantic danger styling, shows the overlay, and focuses cancel when present. `CloseOverlayDialog` clears the deferred action, reenables content, hides the overlay, and restores focus.

- [ ] **Step 4: Convert clear-history confirmation**

Replace the synchronous `MessageBox` in `ClearHistory()` with the shared overlay. The message includes `_history.Items.Count`; only the primary handler calls `_history.Clear()`, `RefreshHistory()`, and `SetStatus("历史记录已清空")`.

- [ ] **Step 5: Convert shortcut-conflict information**

Replace the second `MessageBox` with the same overlay in information mode, hide cancel, label the primary action “返回设置”, and restore focus to `HotKeyTextBox`. Keep rollback to the previous registered shortcut before showing the dialog.

- [ ] **Step 6: Guard global keyboard commands while modal**

At the start of `Window_KeyDown`, if the overlay is visible, handle `Escape` by closing it and return for every other key not handled by the focused dialog button. This prevents `Enter` from calling `CopySelected()` behind the overlay.

- [ ] **Step 7: Verify and commit**

Run build and self-check; expected PASS. Then commit:

```powershell
git add -- src/ClipboardHistory/MainWindow.xaml src/ClipboardHistory/MainWindow.xaml.cs src/ClipboardHistory/SelfCheck.cs
git commit -m "feat: add accessible in-window dialogs"
```

### Task 7: Capture, Inspect, Measure, Package, and Document the Release

**Files:**
- Modify: `src/ClipboardHistory/App.xaml.cs:28-48`
- Modify: `src/ClipboardHistory/UiCapture.cs`
- Modify: `README.md`
- Modify: `GOAL.md`
- Modify: `CONTEXT_SUMMARY.md`
- Modify: `VERIFICATION.md`
- Regenerate: `artifacts/screenshots/main-window.png`
- Regenerate: `artifacts/screenshots/settings-page.png`
- Create or regenerate: `artifacts/screenshots/clear-dialog.png`
- Create or regenerate: `artifacts/screenshots/hotkey-dialog.png`
- Regenerate dark equivalents under `artifacts/screenshots/dark/`
- Regenerate: `artifacts/publish/ClipboardHistory.exe`
- Regenerate: `artifacts/installer/ClipboardHistory-Setup-1.0.0-x64.exe`

**Interfaces:**
- Consumes: Tasks 2-6 and the Task 1 baseline.
- Produces: deterministic light/dark static captures, final performance comparison, rebuilt deliverables, updated hashes, and accurate remaining-risk documentation.

- [ ] **Step 1: Make UI capture deterministic and static**

For `--capture-ui`, force `ThemeMode.Light` and apply the light Monolyth palette. For `--capture-ui-dark`, force `ThemeMode.Dark` and apply the dark palette. Construct `MainWindow(dataPath, manageStartup: false, animationsEnabled: false)` and capture main, settings, clear-dialog, and shortcut-conflict surfaces through the internal preview methods.

- [ ] **Step 2: Run automated verification**

Run:

```powershell
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui .\artifacts\screenshots
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --capture-ui-dark .\artifacts\screenshots\dark
```

Expected: build 0 warnings/0 errors, self-check PASS, and eight non-empty PNG files.

- [ ] **Step 3: Inspect every generated screenshot**

Open all eight PNGs at original detail. Check no clipping, overlap, broken glyph, unreadable text, title-bar hit-area mismatch, background line through text, inconsistent accent, empty middle cell, or light/dark hierarchy regression. Fix and rerun Steps 2-3 until all pass.

- [ ] **Step 4: Manually verify interaction paths on Windows 11**

Verify drag, resize, maximize/restore, double-click title bar, system menu, snap, minimize, close-to-tray, show hotkey, search, selection, double-click copy, Enter copy, right-click commands, settings, `Esc`, both dialogs, focus containment, and system-theme switching. Record exact passes and any environment blocker.

- [ ] **Step 5: Repeat performance measurements**

Use Task 1's exact measurement method and conditions for visible and hidden states, plus minimized if measurable. Confirm visible average CPU is no more than 0.5 percentage points above baseline, hidden/minimized has no sustained animation load, and working-set growth is no more than approximately 15 MB. If a target fails, profile only the new UI layer, remove the offending effect or animation, and repeat.

- [ ] **Step 6: Publish and build the installer**

Run:

```powershell
dotnet publish .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\ClipboardHistory.iss
```

Expected: both commands succeed without warning and produce the named artifacts.

- [ ] **Step 7: Verify reinstall preserves user state**

Hash the existing settings and history files without printing their contents. Exit the running instance, silently install the rebuilt package, rerun the installed `--self-test`, and compare hashes. A settings hash change is acceptable only if a documented application action legitimately changed settings; installation alone must not alter it. Confirm the Run entry remains absent because the user disabled startup.

- [ ] **Step 8: Update user-facing and handoff documentation**

Replace old “静谧雾光 / 独立卡片气泡” descriptions in `README.md`, `GOAL.md`, and `CONTEXT_SUMMARY.md`. Update `VERIFICATION.md` with build results, screenshot hashes, performance comparison, interaction results, publish/installer hashes, Windows 10 and DPI gaps, and the preserved-startup evidence. Do not claim unperformed checks.

- [ ] **Step 9: Run final repository checks**

Run:

```powershell
dotnet build .\src\ClipboardHistory\ClipboardHistory.csproj -c Release
dotnet run --project .\src\ClipboardHistory\ClipboardHistory.csproj -c Release -- --self-test
git diff --check
git status --short
```

Expected: build and self-check pass, no whitespace errors, and only intended source/document changes remain. Generated ignored artifacts must exist even if absent from `git status`.

- [ ] **Step 10: Commit final verification and documentation**

```powershell
git add -- src/ClipboardHistory/App.xaml.cs src/ClipboardHistory/UiCapture.cs README.md GOAL.md CONTEXT_SUMMARY.md VERIFICATION.md
git commit -m "docs: verify the Monolyth UI release"
```

## Implementation References

- Microsoft `WindowChrome`: https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome?view=windowsdesktop-10.0
- Microsoft WPF Fluent theme and `ThemeMode`: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90
- Windows client-area animation preference: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa
