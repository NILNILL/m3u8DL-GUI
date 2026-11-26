# 交接文档：Codex 会话成果（N_m3u8DL-CLI GUI）

> 本文件把 2026-10-21 → 2026-11-10 期间在 Codex 中完成这个项目的**过程与结论**落盘，
> 供 DeepSeek Harness（DSH）及后续任何会话直接继承。它不是进度流水账，
> 只保留「为什么这样做」「坑在哪」「怎么验证」。

| 项 | 值 |
| --- | --- |
| 来源会话 | Codex thread `01a15a21-40db-75c1-8e6e-edf1686d49e1`（另有同 cwd 的 Guardian review 会话，无内容价值） |
| 规模 | 10 轮用户需求 / 47 条对话 / 100 次文件改动 / 499 条命令 |
| 原始证据 | `~/.codex/state_5.sqlite`（threads）、`~/.codex/thread_history_1.sqlite`（逐条 item）、`~/.codex/sessions/2026/10/21/rollout-…01a15a21….jsonl`（12.5 MB） |
| 生成方式 | 从上述 sqlite 提取对话与文件改动，逐条对照当前源码核对后撰写 |
| 核对时间 | 2026-11-25 |
| 标记约定 | ✅ = 已对照当前代码/文件核实；📝 = 仅来自会话记录；⚠️ = 推断或存疑 |

> **2026-11-25 更新**：程序已改名为 **m3u8DL-GUI**，界面按新设计重做（左栏导航 + 设置页 + 双主题色 + 三档外观滑块）。
> §1–§8 记录的是 v1.1（`N_m3u8DL-CLI.exe` 时期）的历史；**文件名、目录与构建命令以 §9 为准**。

---

## 1. 项目是什么

`N_m3u8DL-CLI` 的非官方 **Windows 图形界面外壳**。核心下载引擎（m3u8/MPD 解析、分片下载、ffmpeg 合并）
是原作者 **nilaoda** 的 MIT 代码，本仓库只加 GUI、便携化布局和任务管理。

关键设计：**GUI 不重写引擎**，只做「参数拼装 + 子进程 + 输出解析」。

- 上游仓库：<https://github.com/nilaoda/N_m3u8DL-CLI>
- 本仓库（远程）：<https://github.com/NILNILL/N_m3u8DL-CLI-GUI>，分支 `main`
- 发布形态：整个文件夹可复制到任意 Windows 电脑直接运行（绿色版）

## 2. 当前架构 ✅

```
N_m3u8DL-CLI\
├── N_m3u8DL-CLI.exe            # GUI 外壳（构建产物，同时入库）
├── N_m3u8DL-CLI-engine.exe     # 原版下载引擎
├── ffmpeg.exe                  # 64 位，随包携带
├── README.md                   # 面向使用者
├── Logs\                       # 引擎与 GUI 日志
├── Videos\                     # 视频输出
├── bin\                        # 运行库、图标、构建脚本、GUI 源码
│   ├── build.ps1
│   ├── src\N_m3u8DL-CLI.Gui.cs  # 唯一源码，2459 行单文件
│   ├── *.dll                    # 引擎依赖
│   ├── logo_3Iv_icon.ico
│   └── NO_UPDATE                # 禁止引擎自更新
└── docs\CODEX_HANDOFF.md        # 本文件
```

启动链路 ✅：

1. 无参数启动 → WinForms `MainForm`；带参数启动 → 转发给引擎命令行（保留原 CLI 能力）。
2. `Program.AppDir()`（`:82`）取 EXE 所在目录；`EnginePath()`（`:94`）先找根目录引擎，再找 `bin\`。
3. `EngineStartInfo()`（`:114`）给子进程注入 `PATH = 根目录;bin;<原PATH>`，
   并设置 `NM3U8DL_CLI_GUI=1`、`NM3U8DL_CLI_ROOT=<根目录>`。**这就是便携化的核心。**
4. `WorkingDirectory` 固定为根目录，因此相对路径与 `Videos\`、`Logs\` 行为稳定。
5. `EnsureFolderLayout()`（`:136`）启动时补齐 `bin`、`Logs`，并把旧版残留的 `bin\Logs\*` 搬到根 `Logs\`。

## 3. 十轮需求与结论（按时间顺序）

### 第 1 轮：只下到第一个分段，却显示「下载完成」

**根因（最重要的一条）**：旧版只从系统 `PATH` 找 `ffmpeg`。软件复制到新电脑后，
首段 `.ts` 下载成功，下一步调用 ffmpeg 读视频信息时找不到程序，下载静默中断；
而 GUI 只看子进程退出码，又把残留的首段文件当成功结果 → 显示完成但只有第一段。

**修复** ✅：

- 便携 ffmpeg：注入 `PATH`（`EngineStartInfo`），始终优先用软件目录内的 `ffmpeg.exe`。
- 完成判定不再只看退出码（`PumpStdout` `:1262-1284`）：必须
  `退出码 == 0` **且** 输出文件存在 **且** 大小 > 0，否则判失败并提示「分片可能不完整」。
- 默认线程 32 → 16（`:584`），降低被服务器限流的概率。

### 第 2、3 轮：界面返工两次

- 第 1 版改版过度 + 深色刺眼 → 用户要求**收回改版**，恢复原版比例，深色改低对比度深灰。
- 第 2 版仍与 `N_m3u8DL-CLI-engine.exe` 原生界面差距大 → 最终以引擎原生界面为基准对齐：
  输入框**不留占位文字**、**删除整行「请求头」**、恢复四色信息图例（信息/成功/警告/失败）、
  日志区保持深色底、控件位置与列宽对齐原版。
- **教训**：这个用户对「界面被乱改」极度敏感。任何 UI 改动都应是加法且最小化。

当前配色 ✅（`ApplyTheme()` `:2239`）：深色 `#2B2E32` 窗体 / `#303438` 面板 / `#34383D` 输入框 /
文字 `#D4D7DB`，**禁止纯黑 + 纯白字**。窗口 `920×766`，最小 `860×630`（`:451-452`）。

### 第 4 轮：图标 + 清理制作垃圾

- `bin\logo_3Iv_icon.ico` 同时嵌入 EXE（`/win32icon`）与窗口图标，`build.ps1` 固定图标参数。
- 删除 `preview.png`、`icon-check.png`、`verify-icon.ps1`、`README_ENG.md`、`bin\Logs` 旧日志。

### 第 5 轮：两个重连选项 + 进度单行刷新

- 「达到重连上限后继续下载」`chkInfiniteRetry` → `InfiniteRetry`，不限次数重试。
- 「重连成功后重置次数」`chkResetRetryOnReconnect` → 连续重连计数在「重连后进度继续增长」时清零（`:1318-1326`）。
- 上限常量 `MaxReconnectAttempts = 5`（`:379`），重连间隔 3 秒（`ScheduleReconnect` `:1354`）。
- 两者同时勾选时，以「不限次数」优先。
- **引擎侧技巧** ✅：勾任一项时向引擎传 `--retryCount 1`（`:1214-1217`），
  让引擎自己快速失败返回，由 GUI 的定时器接管重试节奏——否则引擎内部重试会与 GUI 叠加。
- 进度信息改为**每任务固定一行、原位替换**（`UpdateProgressLog` `:1966`，靠 `progressLogLines` 记录
  每行起止偏移并顺移后续行）；解析/重连/合并/失败等仍各占新行。日志超过 200000 字符时整块清空。

### 第 6 轮：目录重构 + 底部参数不再重叠

- 目标布局：根目录只留 2 个 EXE + `ffmpeg.exe` + `README.md` + `Logs\` + `Videos\` + `bin\`。
- **坑** ✅：本盘是 **exFAT**，`mklink /J` 目录联接不可用 → 放弃链接方案，
  改用「注入 `PATH` + 把 `bin\Logs` 真实搬到根 `Logs`」实现同样的效果。
- 底部参数区重新分配宽度：速度显示剩余秒数、进度显示 `当前/总大小`、状态右移。

### 第 7 轮：恢复了被误删的用户 txt

`新建 文本文档.txt` 曾被清理流程误删，按内容还原。**教训：清理前必须先确认哪些是用户文件。**
⚠️ 当前根目录**看不到**该 txt（可能用户后来自己删了）——如需保留请先确认。

### 第 8 轮：git 推送事故

本地 `main` 只有暂存文件、没有任何提交，`git push` 报「源引用规范 main 与任何不匹配」。
处理：以远程 `origin/main` 为父建立本地历史 → 提交 `3230b06 Update_GUI_and_portable_layout` → 普通推送（非强推）。
**教训**：这个仓库曾经是「有 .git 但无提交」的半初始化状态，动手前先 `git log` 确认。

### 第 9 轮：窗口置顶

`chkTopMost`（`:536`）→ `TopMost`，状态持久化；系统安全桌面/UAC/任务管理器仍在其上。

### 第 10 轮：置顶边框裁切 + 下移 6px + 勾选记忆

- 顶部主题栏增高，让「窗口置顶」复选框边框完整显示；主体内容整体下移约 6 像素。
- 持久化项：主题、置顶、完成后删分片、无限重连、重连重置次数。
- 存储位置 ✅：`%LOCALAPPDATA%\N_m3u8DL-CLI\settings.ini`（`settingsPath` `:423`，UTF-8 读写，
  行格式 `key=value`）。
  ⚠️ 第 10 轮的会话描述写的是「当前用户的 `N_m3u8DL-CLI\settings.ini`」，实际代码在 `LocalApplicationData` 下，
  以代码为准。

## 4. 关键实现位置（源码 `bin/src/N_m3u8DL-CLI.Gui.cs`）

| 关注点 | 位置 | 说明 |
| --- | --- | --- |
| 目录/引擎定位 | `:82` / `:94` / `:114` | 便携化的三处根基 |
| 布局补齐、日志搬迁 | `:136` / `:160` / `:203` | 每次启动与退出都会尝试搬迁 |
| 参数拼装 | `:1189` | `--workDir`、`--saveName`、`--proxyAddress`、`--maxThreads`、`--minThreads`、`--maxSpeed`、`--retryCount`、`--enableDelAfterDone` |
| 完成判定 | `:1262-1284` | 退出码 + 非空输出 |
| 重连状态机 | `:1287-1410` | `HandleProcessExit` → `ScheduleReconnect` |
| 删除任务与文件 | `:1678` / `:1724` / `:1756` | 确认框列出待删项；`IsSafeOutputPath`（`:1822`）拒绝工作目录外路径 |
| 进度行刷新 | `:1966` | 偏移量记账，避免整块重排 |
| 设置读写 | `:2122` / `:2193` | 容错读取，缺项用默认值 |
| 主题 | `:2239` / `:2372` | 调色板 + DWM 深色标题栏（属性 20，回退 19） |

删除任务会清理：最终视频、`<名称>(Audio)`/`<名称>(Subtitle)` 目录、
`名称 + 扩展名` 命中的 `.mp4/.mkv/.ts/.flv/.m4a/.aac/.srt`，以及引擎的临时目录与 `*.tsdownloading` 残留。

## 5. 构建与验证 ✅

```powershell
# 在仓库根目录
powershell -ExecutionPolicy Bypass -File .\bin\build.ps1
```

- 编译器：`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（缺失则回退 32 位路径）。
- 目标：`winexe`、`anycpu`、`optimize+`，图标 `bin\logo_3Iv_icon.ico`。
- **输出是 `N_m3u8DL-CLI.new.exe`，不是直接覆盖**。必须手动替换 `N_m3u8DL-CLI.exe`。
- **坑**：正在运行的 GUI 会锁住 EXE。替换前先确认没有下载在跑、关闭窗口；
  不要结束引擎进程，也不要删 `Videos\`。

验证方式（历史做法）：本地 `testdata\sample.m3u8` 造 3 段测试视频，跑完整 GUI 流程确认合并成功；
再离屏跑 `GuiHarness` 类型的小程序验证主题、置顶、勾选持久化。
⚠️ 这些 `tools\*` 脚手架在清理中被删除，**当前仓库没有自动化测试**，改 UI 只能手动验证。

## 6. 已知坑与限制

1. **exFAT 盘** ✅：不能建目录联接/符号链接；任何「用链接改布局」的方案都不可行。
2. **编码** ✅：`README.md`、`Gui.cs`、`build.ps1` 都是 **UTF-8 无 BOM**。
   Windows PowerShell 5.1 的 `Get-Content` 默认按 ANSI 读会乱码，必须 `-Encoding UTF8`；
   读取用支持 UTF-8 的工具。
3. **进度行偏移**：`UpdateProgressLog` 依赖字符偏移记账，任何在中间插入/删除日志文本的改动都可能错位，
   改这块要连带测试「多任务并发 + 日志清空」两个场景。
4. **DPI**：布局是绝对坐标（`Point`/`Size`），高 DPI 下容易出现标签与输入框重叠；
   历史修过两次（线程/限速/代理位置、底部参数区宽度）。再改布局务必在 100%/125%/150% 下都看一眼。
5. **深色标题栏** ⚠️：`DwmSetWindowAttribute` 属性 20/19 是 Win10 1809+ 才支持，
   README 声明支持 Win7 SP1 —— Win7 上不会崩（有 try/catch），但标题栏不会变深。
6. **README 与真实状态有偏差**：README 写「构建脚本会生成 `.new.exe`」，与 `build.ps1` 一致；
   但 README 未提及 `docs\` 目录，也没写 `tools\` 已删除。改结构时同步更新 README。
7. **工作区未提交** ✅：`N_m3u8DL-CLI.exe`、`README.md`、`bin/src/N_m3u8DL-CLI.Gui.cs` 均有未提交改动，
   `Logs/` 未被跟踪。即「磁盘上的代码比 `origin/main` 新」，提交前先 diff 确认。

## 7. 待办候选（未完成 / 待确认）

- [ ] 确认 `新建 文本文档.txt` 是否需要恢复（第 7 轮还原过，现已不在根目录）。
- [ ] 提交当前三处未提交改动（exe / README / Gui.cs），避免再次出现「代码只在磁盘上」。
- [ ] 视需要恢复最小回归脚手架（一个离屏 GUI 测试 + 一个 3 段测试 m3u8），否则每次改 UI 全靠肉眼。
- [ ] 高 DPI 适配若仍被反馈重叠，考虑把绝对坐标布局改为 `TableLayoutPanel`（属于结构性改动，需先征得同意）。

## 8. 给后续接手者（含 DSH 会话）的阅读顺序

1. 仓库根 `AGENTS.md` —— 铁律与构建流程（每个会话自动加载）。
2. 本文件 —— 历史决策与坑。
3. `bin/src/N_m3u8DL-CLI.Gui.cs` —— 唯一源码，先读 `Program` 段（`:16-280`）再读 `MainForm`。
4. `README.md` —— 对外说明，改行为后要同步。
## 9. v1.2 改版记录：m3u8DL-GUI（2026-11-25）

### 9.1 做了什么

| 项 | 结果 |
| --- | --- |
| 改名 | 程序名 `m3u8DL-GUI`；EXE 改为 `m3u8DL-GUI.exe`；窗口标题、状态栏、关于页同步 |
| 主题 | 明暗两套基底 × 两种强调色（大肥鱼蓝 `#2E7CF6` / Claude 橙 `#D97757`） |
| 设置入口 | 顶栏右侧设置图标 + 左栏齿轮，两处都能进设置页 |
| 设置页 | 左侧分类「主题 / 关于」；主题页内左侧主题色卡、右侧外观模式滑块 |
| 外观模式 | 三档滑块：浅色 / 深色 / 跟随系统，滑块指数缓动，点击任意档位立刻滑动 |
| 视觉 | 圆角卡片（14）、圆角输入框（9）、圆角按钮（10）、自绘复选框、圆角进度条、页面左右滑动切换 |
| 设置迁移 | 新路径 `%LOCALAPPDATA%\m3u8DL-GUI\settings.ini`，首启自动复制旧版配置 |

### 9.2 技术栈决策：为什么仍然用 WinForms

用户允许"必要时引入新技术栈"，评估后仍选择 **WinForms + 自绘控件层**：

- WPF 的圆角/动画确实更省事，但本机没有 MSBuild 与 XAML 编译链（只有 `csc.exe` 与运行时程序集），
  用命令行手工编译 XAML 非常脆弱；引入 .NET SDK 构建又会破坏"整个文件夹复制即用"的绿色形态。
- WebView2 需要额外运行时，直接排除。
- GDI+ 双缓冲自绘在 60fps 下足够画出圆角、渐变、缓动，且零依赖。

**结论**：不换框架，而是补一层自绘控件与动画泵（`m3u8DL-GUI.Controls.cs`）。

### 9.3 新的源码结构

`bin/src` 由单文件拆成 4 个文件，`MainForm` 用 `partial` 跨文件拆分：

| 文件 | 职责 |
| --- | --- |
| `m3u8DL-GUI.Theme.cs` | `UiPalette`（明暗 × 强调色）、`UiMetrics`（DPI 缩放）、`UiDraw`（圆角路径/混色/缓动） |
| `m3u8DL-GUI.Controls.cs` | `AnimBus` 动画泵 + `SoftCard`/`SoftButton`/`SoftIconButton`/`RailButton`/`SideItem`/`SoftCheckBox`/`SoftField`/`SegmentedSwitch`/`AccentSwatch`/`SoftBar`/`DotPanel` |
| `m3u8DL-GUI.Ui.cs` | 界面装配、布局计算、页面滑动、`ApplyTheme`、ListView 自绘 |
| `m3u8DL-GUI.Main.cs` | 与 v1.1 完全一致的业务逻辑（引擎、解析、重连、删除、设置） |

业务逻辑是**原样搬运**（按行号切片拼接），只改了命名空间与程序名，减少回归风险。

### 9.4 关键发现：这台机器是 125% 缩放

排查"复选框文字糊成一团"时定位到：系统 `AppliedDPI = 120`（125%），而旧版只调用 `SetProcessDPIAware()`
声明 DPI 感知，所有控件尺寸却是固定像素 → 字体按磅值放大 25%、容器没放大，文字被挤爆。

处理：新增 `UiMetrics.Scale = DpiX / 96`，**所有布局像素统一走 `UiMetrics.S()`**，
字体保留磅值由系统放大。布局基准改为 96 DPI 设计单位（窗体 1000×812）。

> 教训：截图验证时如果用 `CopyFromScreen`，在 DPI 感知进程里会因虚拟化而错位、叠加；
> 改用 `Form.DrawToBitmap` 才是 1:1 结果（本仓库 `tools\ShotHarness.cs` 已按此实现）。

### 9.5 动画与交互参数

- 全局 `AnimBus`：15ms 计时器，只有存在进行中的动画才运行，空闲自动停（不常驻占 CPU）。
- 悬停/按下：指数逼近（`0.26` / `0.38` 系数），约 120–160ms 收敛。
- 三档滑块：`0.24` 系数指数逼近，约 220ms 落位；标签用透明度插值切换粗细。
- 页面切换：`EaseOut` + 0.18 系数，约 240ms；两页在 `pageHost` 内左右滑动。
- 主题色切换：立即换调色板（不插值），避免出现"灰半截"的中间态。

### 9.6 本次验证

- `tools\Func.cs` 冒烟测试 10 项全过：设置页滑动落位、深色生效、滑块索引、强调色切换、
  设置文件内容、返回下载页、重启后主题与强调色持久化。
- `tools\ShotHarness.cs` 产出 5 张状态图（浅色/深色 × 双强调色 × 下载页/设置页）逐一目检。
- 发布物 `m3u8DL-GUI.exe` 冷启动冒烟：窗口标题正确、可正常关闭、首启迁移旧设置成功。

### 9.7 已知限制与待办

- 原生 `NumericUpDown` 微调按钮在深色下会露白色方块，已隐藏，改用滚轮/方向键修改数值。
- 任务列表为空时没有空态文案；列表表头最后一列可能留下少量空白。
- 未做高对比度（系统辅助功能）适配，也未做 200% 以上 DPI 的专门测试。
- 旧名 `N_m3u8DL-CLI.exe` 已删除；若用户桌面快捷方式指向旧文件名，需要重新创建。

## 9.8 修复重影 + 换图标 + 1.3.0（2026-11-25 晚）

### 现象

用户在 125% 缩放下看到的截图里，红框区域内出现「文字叠字、位置错乱」：
「窗口置顶」旁边糊着别处的文字、三个复选框的文字互相叠加、数值框右侧出现黑块。

### 根因（两层，缺一不可）

1. **自绘控件没有清屏。** `SoftCheckBox` / `SoftCard` / `SoftField` / 按钮等 `OnPaint`
   只画自己要画的内容，没有先把整块区域刷成底色。WinForms 在有
   `ControlStyles.AllPaintingInWmPaint` 时不保证替你擦背景，于是上一帧甚至别的控件留下的
   像素就留在那里；再叠上新文字，就成了「重影」。
2. **整窗没有一次性合成。** 子窗口较多且相互覆盖时，逐窗绘制会出现「谁后画谁赢」的串色。
   加上 `WS_EX_COMPOSITED`（`MainForm.CreateParams`）让整窗自下而上合成后消失。

`DrawToBitmap` 走内存 DC，每个控件都是全新位图，所以**离屏截图完全看不出这个问题**——
这也是上一轮「自测通过」却仍被用户发现的原因。教训：界面验收必须抓真实窗口。

### 修复

- 新增 `AnimControl.PaintSurface(Graphics)`，所有自绘控件 `OnPaint` 第一行先铺底色；
  `SoftCard` / `SoftField` / `SoftCheckBox` 手动 `FillRectangle(BackColor)`。
- 全项目移除 `BackColor = Color.Transparent`（透明底在自绘体系里只会带来不确定性），
  卡片/输入框的圆角外侧改由不透明底色填充。
- 表单启用 `WS_EX_COMPOSITED`。
- 原生 `NumericUpDown` 换成自实现的 `SoftNumber`（继承 `TextBox`，自带 `Value/Minimum/Maximum`、
  方向键与滚轮步进）。原生微调按钮在深色下露白块、隐藏后又留下黑块，直接不用它。
- 页面切换改为**时间驱动**（固定 220ms，`pageStartT + (target-start)*k`，再套 `EaseOut`），
  不再依赖帧数，重绘慢的机器上也不会卡在中途。
- 图例圆点改由主题调色板着色（之前被 `StyleTree` 覆盖成背景色后消失）。

### 图标与版本

- 新图标 `bin\m3u8dl-gui.ico`（16/24/32/48/64/128/256 多尺寸）+ `bin\m3u8dl-gui.png`；
  蓝底圆角方块 + 白色下载箭头与托盘，由 Pillow 生成。旧 `logo_3Iv_icon.ico` 已删除。
- 侧栏 logo 用 PNG 加载（ICO 的 48px 帧经 `Icon.ToBitmap()` 在 PictureBox 里会花屏）。
- `AppVersion` 提升到 **1.3.0**，状态栏与「关于」页自动跟随。
- 深色标题栏改到 `Shown` 时再设置一次 `DwmSetWindowAttribute`，Win10/11 深色标题栏才生效。

### 验证

- `tools\Func.cs` 10 项全过（页面落位、深色、滑块、强调色、设置写入、重启持久化）。
- `tools\CaptureWindow.cs` 抓真实窗口：浅色/深色、两种强调色、设置页均无重影、无黑块。
- 启动冒烟：标题 `m3u8DL-GUI`、版本 1.3.0、首启迁移旧设置正常。

## 9.9 目录重构：拆掉万能的 bin\（2026-11-25 深夜）

### 目标

原来所有东西都塞在 `bin\`（DLL、图标、源码、构建脚本），根目录还有引擎。
现在按用途拆开，根目录只留用户真正会碰的东西。

### 新布局

```text
m3u8DL-GUI\
├── m3u8DL-GUI.exe      主程序
├── ffmpeg.exe          随包 ffmpeg
├── README.md
├── Logs\  Videos\      运行数据
├── runtime\            引擎 + 7 个 DLL + NO_UPDATE
├── assets\             m3u8dl-gui.ico / .png
├── src\                build.ps1 + 5 个 .cs
├── docs\  tools\       开发文档与验证工具
```

### 路径解析（都做了旧布局兼容）

| 用途 | 顺序 |
| --- | --- |
| 引擎 | `runtime\` → 根目录 → 旧 `bin\` |
| 图标 | `assets\` → 旧 `bin\` |
| 引擎依赖查找 | 引擎与 DLL 必须同目录（同放在 `runtime\`，AppDomain 基目录即引擎目录） |
| 子进程 PATH | 前置「根目录 + runtime」，保证 ffmpeg 优先用随包那份 |

### 这次才发现的引擎行为

- 引擎的日志不是写到工作目录，而是写到**自己所在目录下的 `bin\Logs`**。
  以前引擎在根目录，所以日志落在 `<根>\bin\Logs`；现在它在 `runtime\`，日志落在 `runtime\bin\Logs`。
- 引擎启动时还会在自己目录下创建空的 `Videos\`。

处理：`MoveEngineLogsToRoot()` 现在同时扫描 `runtime\bin\Logs`、`runtime\Logs`、旧 `bin\Logs`，
把日志搬到根 `Logs\`；`RemoveEmptyEngineLogDirectory()` 顺手删掉搬空的
`runtime\bin\Logs`、`runtime\bin`、`runtime\Videos`。实测跑完一次失败下载后，
`runtime\` 里只剩 9 个文件、没有任何子目录。

### 构建

脚本从 `bin\build.ps1` 移到 `src\build.ps1`，它会自己推导根目录，图标取 `assets\m3u8dl-gui.ico`，
编译 `src\*.cs` 输出到根目录的 `m3u8DL-GUI.new.exe`。

### 验证

- 路径自检（临时工具）：`EnginePath` = `runtime\N_m3u8DL-CLI-engine.exe`、图标/Logo 均命中 `assets\`。
- 端到端：带参数启动 `m3u8DL-GUI.exe <坏地址>` → 引擎从 runtime 正常拉起（打印版本横幅）→
  退出后日志出现在根 `Logs\`，`runtime\` 无残留子目录。
- `tools\Func.cs` 界面与持久化测试仍全过。

### 注意

- `runtime\` 里的文件不要单独挪动，引擎和 DLL 必须在一起。
- 分发时 `src\`、`docs\`、`tools\` 可以删除，不影响运行。

## 9.10 FFmpeg 来源 / 多语言 / 检查更新（2026-11-26）

### 版本与仓库

- 版本改为 **1.2.0**（`src\AssemblyInfo.cs` 单一来源，界面与 EXE 属性同步）。
- 仓库地址改为 <https://github.com/NILNILL/m3u8DL-GUI>；该仓库已有 1.1.0 的 Release。

### FFmpeg 来源（设置 → FFmpeg）

三选一，设置项 `ffmpeg_mode=bundled|custom` 与 `ffmpeg_dir=<文件夹>`：

| 模式 | 行为 |
| --- | --- |
| 使用软件目录 | 默认；引擎 PATH 首位是程序根目录，命中随包的 `ffmpeg.exe` |
| 自定义目录 | 选择包含 `ffmpeg.exe` 的文件夹；选到没有该文件的目录会提示重选 |
| 自动检测 | 遍历系统 `PATH` 找 `ffmpeg.exe`，命中后填入并切到自定义模式 |

关键实现：`Program.CustomFfmpegDir`（静态）由 `ApplyFfmpegSelection()` 同步，
`EngineStartInfo()` 拼 PATH 的顺序是 **自定义目录 → 程序根目录 → runtime → 系统 PATH**。
面板底部实时显示最终会用的 `ffmpeg.exe` 绝对路径，文件不存在时用危险色标记。

### 多语言

- 新增 `src\m3u8DL-GUI.Lang.cs`：`Map` 以**简体中文原文为键**，值是该句的繁体与英文。
- 构建完成后 `CaptureOriginalTexts()` 记住每个控件的原始文字，`ApplyLanguage()` 统一翻译；
  之后再切语言不会出现「中文翻译成英文再翻回去」的漂移。
- 需要特殊处理的三类：`SegmentedSwitch` 的档位标签、`AccentSwatch` 的标题/说明、
  任务列表列头——它们没有 `Text` 属性，改为显式调用。
- **布局随文字宽度重排**：英文比中文长，`LayoutParams()` 改成先用
  `TextRenderer.MeasureText` 量说明文字宽度，再把输入框依次右推；三个复选框按各自宽度依次排布；
  图例（Info/Success/Warning/Failed）也要重排，否则会挤在一起。
- 任务状态内部仍保存简体（`等待/重连中/完成/失败`），显示时走 `Lang.Status()` 翻译，
  `StateColor()` 的判断逻辑不受影响。
- 语言解析：`跟随系统` 看 `CurrentUICulture`——`zh-Hant/TW/HK/MO` 用繁体，其他 `zh` 用简体，
  非中文一律英文。

### 检查更新

- 位置：设置 → 关于 → 「检查更新」。
- **不用 api.github.com**：未登录时 60 次/小时限额，实测直接 403。改为请求
  `https://github.com/<owner>/<repo>/releases/latest`，跟着 301 跳到 `/releases/tag/1.1.0`，
  从最终 URL 里取 tag，再与 `AppVersion` 按 `x.y.z` 比较。
- 结果三态：已是最新（显示当前版本）/ 发现新版本（显示标签并提供「打开下载页」）/ 暂无发布版本。
- 仓库没有 Release 时 GitHub 返回 404，按「暂无发布版本」处理，不当作错误。

### README

重写为面向使用者：功能表、设置说明（主题 / FFmpeg / 语言 / 关于）、
**只含运行所需文件的目录结构**、以及**上传 GitHub 时的完整仓库结构**（额外含 src/docs/tools）。

### 验证

- 更新检查实跑：`tag=1.1.0`、`newer=false`（当前 1.2.0）→ 界面显示「已是最新版本」。
- PATH 自动检测实跑：命中 `E:\Software_DevKit\ffmpeg-9.0.2-essentials_build\bin`。
- 自定义目录优先：`EngineStartInfo` 的 PATH 首段确实是自定义目录，清空后回到程序根目录。
- 界面渲染：主题 / FFmpeg / 语言 / 关于 四个面板，以及简中、繁中、英文三种状态逐一截图检查，
  英文下说明文字不再被截断、复选框不再重叠。
- `tools\Func.cs` 仍全过。

### 已知限制

- 日志区里程序自己写的行（如「新建任务」）还是中文，引擎输出本身也是中文；界面文案已全部本地化。
- 切换语言不会回溯翻译「历史日志行」和已存在的任务状态文本，新产生的内容才是新语言。

## 9.11 侧栏误选中 / 线程限速记忆 / 语言重启（2026-11-26 晚）

### 1. 设置页三个分类同时高亮

**现象**：每次进入设置，「主题 / FFmpeg / 语言」三项一起是选中态，只有「关于」正常。

**根因**：`SideItem` 类里 `public bool Active = true;` 默认就是选中；
`BuildSettingsPage` 只给 `sideAbout` 显式设了 `false`，而 `ShowSettingsPane()` 从没在构建完成后调用过一次。

**修复**：
- `SideItem.Active` 默认值改为 `false`（其它调用点本来就显式赋值）。
- `BuildSettingsPage()` 末尾调用 `ShowSettingsPane(0)`，保证初始只选中「主题」。

### 2. 线程 / 限速记忆

- 新增设置键 `threads`、`max_speed`，`Main` 里用 `threadsValue` / `speedValue` 承接。
- `BuildParamsCard` 建控件时就回填（先设 `Minimum/Maximum` 再设 `Value`，否则会被夹到边界）。
- 两个 `SoftNumber`（自绘数字框）订阅 `TextChanged`：`initialized` 为真且能解析时才写盘，
  所以启动回填不会触发保存循环。
- 实测：改成 24 线程 / 512 KB/s，销毁窗口再新建，值仍然是 24 与 512，`settings.ini` 里也有对应两行。

### 3. 语言切换改为「保存 + 重启」

**原因**：即时 `ApplyLanguage()` 虽然能翻译绝大多数控件，但自绘控件里有一部分文字是构造时缓存或
动态拼接的（`SegmentedSwitch` 档位、`AccentSwatch` 标题、ListView 列头、状态文案），
容易出现「一半新语言，一半旧语言」的状态。

**现在的流程**：
1. 点语言项 → 若有下载任务在跑，先提示「请先停止正在进行的下载任务」并回退；
2. 否则弹窗「切换语言需要重新打开软件才能生效，是否立即重启？」；
3. 选「是」→ `CommitLanguageChange()` 写盘 → `RestartApplication()` 起新进程并退出当前进程；
4. 选「否」→ `RevertLanguageSelection()` 只把左侧选中态刷回当前设置，配置不动。

为便于测试，逻辑拆成 `CommitLanguageChange` / `RevertLanguageSelection`，
UI 只负责弹窗与重启。实测：写入 `lang=en` 后重新创建窗口，按钮变成 `Start`、侧栏变成 `Theme`；
调用回退不会改动 `Lang.Setting`。

### 验证

- 启动侧栏选中态：主题=True / FFmpeg=False / 语言=False / 关于=False。
- 线程与限速跨启动保留（24 / 512）。
- `lang=en` 重启后界面为英文。
- `tools\Func.cs` 10 项仍全过。
