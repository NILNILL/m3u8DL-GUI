# AGENTS.md — m3u8DL-GUI

面向所有在本仓库工作的 AI 代理（DSH / Codex / 其他）。**先读这里，再读 `docs/CODEX_HANDOFF.md`。**

## 项目

`N_m3u8DL-CLI` 的 Windows 图形界面外壳，程序名 **m3u8DL-GUI**（WinForms，.NET Framework）。
下载引擎是原作者 **nilaoda** 的 MIT 代码（文件名固定为 `N_m3u8DL-CLI-engine.exe`，不要改名）。
GUI **不重写引擎**：拼命令行 → 起子进程 → 解析 stdout。

- 远程：`https://github.com/NILNILL/m3u8DL-GUI`，分支 `main`
- 语言：与用户交流、界面文案、注释统一用**简体中文**

## 目录与源码分工

```text
m3u8DL-GUI.exe      主程序（根目录）
ffmpeg.exe          随包 ffmpeg（根目录）
runtime\            下载引擎 + 全部 DLL + NO_UPDATE（不要移出）
assets\             窗口/侧栏图标 m3u8dl-gui.ico / .png
src\build.ps1       构建脚本
src\*.cs            源码，见下表
docs\ tools\       开发文档与验证工具
```

源码分工：

```text
src\m3u8DL-GUI.Theme.cs     调色板（明暗 × 双强调色）、UiMetrics（DPI 缩放）、绘图原语
src\m3u8DL-GUI.Controls.cs  自绘控件层：圆角卡片/按钮/复选框/输入框(SoftField)/数字框(SoftNumber)
                             /三档滑块/主题色卡/进度条/动画泵
src\m3u8DL-GUI.Ui.cs        界面装配：左栏导航、顶栏、下载页、设置页、页面滑动、主题应用
src\m3u8DL-GUI.Main.cs      业务：引擎调用、输出解析、重连状态机、删除清理、设置读写
src\m3u8DL-GUI.Lang.cs      简中/繁中/英文文案表（键是简体原文）
src\m3u8DL-GUI.Update.cs    GitHub Releases 版本检查
src\AssemblyInfo.cs         版本号唯一来源（当前 1.2.0）
```

## 铁律（违反过就会被要求返工）

1. **颜色只能来自 `UiPalette`。** 不要在任何界面代码里写死颜色字面量（日志区除外，它固定深色）。
   新增控件必须实现 `ApplyPalette(UiPalette)`，否则深色下会露白底。
2. **所有像素尺寸必须经过 `UiMetrics.S()` / `UiMetrics.SF()` 缩放。** 界面按 96 DPI 设计，
   运行时按系统 DPI 放大；写死像素会在 125%/150% 缩放下把文字挤出输入框。
   字体用**磅值**（系统会自动按 DPI 放大），不要再乘缩放系数。
3. **保持设计语言。** 卡片圆角 14、输入框 9、按钮 10（均在 96 DPI 设计单位下），
   动画统一走 `AnimBus`（15ms 泵）+ `UiDraw.EaseOut`，时长感 180–300ms；不要加突兀的即时跳变。
4. **深色模式不许用纯黑 + 纯白字**：窗体 `#171A1F`、卡片 `#21252B`、文字 `#E8EBEF`。
5. **不许依赖系统 PATH 里的 ffmpeg**：必须优先命中程序根目录的 `ffmpeg.exe`
   （`EngineStartInfo` 把「根目录;`runtime`」前置到子进程 PATH）。
6. **不许删用户文件**：`Videos\`、`Logs\`、`runtime\NO_UPDATE` 一律保留；清理前先确认是不是用户的东西。
7. **完成 ≠ 退出码 0**：判定成功必须同时满足「退出码 0 + 输出文件存在 + 大小 > 0」。
   这是「只下到第一段却显示完成」那个 bug 的修法，不要退回去。
8. **引擎必须留在 `runtime\`，文件名不可改动**：`Program.EnginePath()` 依次找
   `runtime\` → 根目录 → 旧版 `bin\`，引擎的 DLL 也要和它同目录（不要拆开）。
9. **新增界面文案必须进语言表**：`Lang.Map` 的键是简体原文，`Add(简体, 繁体, English)` 三条都要写。
   漏写不会报错，但切到繁体/英文时该控件会一直显示中文。
10. **自绘控件必须自己清屏**：`OnPaint` 开头要调 `PaintSurface(g)`（或手动 `FillRectangle(BackColor)`）。
   漏掉这一步会出现「文字重影 / 黑块」——别的控件或上一帧的像素会留在自己的区域里。
   窗体还额外开了 `WS_EX_COMPOSITED`（见 `MainForm.CreateParams`），两者缺一不可。

## 构建与发布

```powershell
powershell -ExecutionPolicy Bypass -File .\src\build.ps1
```

- 用 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 编译 `src\*.cs`，输出 **`m3u8DL-GUI.new.exe`**。
- 替换前先关闭正在运行的 GUI（EXE 被锁会复制失败）；不要结束引擎进程，不要删 `Videos\`。
- 替换 = 把 `m3u8DL-GUI.new.exe` 改名为 `m3u8DL-GUI.exe`。
- 改完界面必须**实际编译成功**再声称完成；只改源码不编译等于没做。

## 验证方式

`tools\` 下有三个开发期工具（不随软件发布，也不参与 `src\build.ps1`）：

- `tools\Func.cs`：功能冒烟测试，验证页面切换动画落位、主题切换、设置写入与重启后持久化。
- `tools\ShotHarness.cs`：离屏截图，用 `DrawToBitmap` 输出各主题状态 PNG（看不出重影问题）。
- `tools\CaptureWindow.cs`：抓真实窗口画面（`PrintWindow`），窗口被遮挡时也能用。

编译方式（示例）：

```powershell
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = Get-ChildItem .\src -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /target:exe /main:Func /platform:anycpu /out:.\tools\func.exe `
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll /reference:System.Xml.dll ($src + ".\tools\Func.cs")
```

改界面后至少检查：浅色、深色、两种强调色、设置页滑块三个档位；以及 100% / 125% DPI 下文字不挤压。

## 两个易踩的功能点

- **FFmpeg 来源**：`Program.CustomFfmpegDir` 非空且其中存在 `ffmpeg.exe` 时，
  `EngineStartInfo` 会把它排在 PATH 最前面（其次才是程序根目录与 `runtime`）。
  设置在 `ffmpeg_mode` / `ffmpeg_dir` 两个键里，改完必须调用 `ApplyFfmpegSelection()` 同步。
- **线程 / 限速记忆**：`threads`、`max_speed` 两个键，输入框 `TextChanged` 时同步保存；
  启动时在 `BuildParamsCard` 里回填，注意先设 `Minimum/Maximum` 再设 `Value`。
- **更新检查**：走 `https://github.com/<owner>/<repo>/releases/latest` 的 301 跳转取 tag，
  **不要换成 api.github.com**——未登录时 60 次/小时会被限流（实测返回 403）。

## 数据与配置位置

- 设置：`%LOCALAPPDATA%\m3u8DL-GUI\settings.ini`（旧版 `N_m3u8DL-CLI` 目录会在首启自动迁移）
- 版本号：唯一来源是 `src\AssemblyInfo.cs` 的 `AssemblyVersion`；界面里的 `AppVersion` 从程序集读取
- 语言：`src\m3u8DL-GUI.Lang.cs` 的 `Map` 以**简体中文原文为键**，切换时把控件原始文字翻译一遍。
  **切换语言走「保存 + 重启」**（`CommitLanguageChange` → `RestartApplication`），
  不要再改回即时 `ApplyLanguage`：自绘控件里有一部分文字是缓存/动态生成的，
  即时切换容易出现「一半新语言、一半旧语言」的中间态。用户在对话框选「否」时调用 `RevertLanguageSelection` 回退。
- 视频：`<程序目录>\Videos\`
- 日志：`<程序目录>\Logs\`；引擎实际写在 `<引擎目录>\bin\Logs`，程序在启动/退出时自动搬走并删空目录

## 编码与工具注意

- 仓库文本文件是 **UTF-8 无 BOM**。Windows PowerShell 5.1 的 `Get-Content` 默认按 ANSI 读会乱码，
  必须 `-Encoding UTF8`。
- 本工程位于 **exFAT** 盘：不能用 `mklink /J`、符号链接改布局。
- 截图验证要注意：`DrawToBitmap` 走内存 DC，**看不出**真实窗口的重影问题；窗口被遮挡时用
  `tools\CaptureWindow.cs`（`PrintWindow` + `PW_RENDERFULLCONTENT`）抓真实画面。

## 文档

- 历史决策、踩过的坑、待办：`docs/CODEX_HANDOFF.md`
- 对外说明：`README.md`（改行为后同步更新，不要写成变更流水账）
