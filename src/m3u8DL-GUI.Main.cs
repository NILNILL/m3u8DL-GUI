// m3u8DL-GUI —— 图形界面外壳
// 下载引擎（N_m3u8DL-CLI-engine.exe）来自原作者 nilaoda，遵循 MIT 许可证。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace M3u8DlGui
{
    internal static class Program
    {
        private static readonly object LogMoveLock = new object();

        /// <summary>用户自定义的 ffmpeg 目录；为空表示用软件目录内的那份。</summary>
        internal static string CustomFfmpegDir = "";

        [STAThread]
        private static void Main(string[] args)
        {
            EnsureFolderLayout();
            try
            {
                NativeMethods.SetProcessDPIAware();
            }
            catch
            {
            }

            if (args != null && args.Length > 0)
            {
                try
                {
                    NativeMethods.AttachConsole(-1);
                }
                catch
                {
                }
                Environment.ExitCode = RunEngine(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static int RunEngine(string[] args)
        {
            try
            {
                PrepareEngineLogDirectory();
                ProcessStartInfo startInfo = EngineStartInfo(QuoteArguments(args));
                startInfo.EnvironmentVariables.Remove("NM3U8DL_CLI_GUI");
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return -1;
                    }
                    process.WaitForExit();
                    MoveEngineLogsToRoot();
                    RemoveEmptyEngineLogDirectory();
                    return process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Lang.Translate("无法启动下载引擎：") + "\r\n" + ex.Message,
                    "m3u8DL-GUI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return -1;
            }
        }

        internal static string AppDir()
        {
            try
            {
                return Path.GetDirectoryName(Application.ExecutablePath);
            }
            catch
            {
                return Environment.CurrentDirectory;
            }
        }

        /// <summary>引擎所在目录：新布局在 runtime\，旧布局退回根目录。</summary>
        internal static string RuntimeDir()
        {
            string root = AppDir();
            string runtime = Path.Combine(root, "runtime");
            if (Directory.Exists(runtime))
            {
                return runtime;
            }
            return root;
        }

        internal static string EnginePath()
        {
            string root = AppDir();
            string[] candidates = new string[]
            {
                Path.Combine(root, "runtime", "N_m3u8DL-CLI-engine.exe"),
                Path.Combine(root, "N_m3u8DL-CLI-engine.exe"),
                Path.Combine(root, "bin", "N_m3u8DL-CLI-engine.exe")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            throw new FileNotFoundException(
                "缺少下载引擎 runtime\\N_m3u8DL-CLI-engine.exe，请保持软件目录完整。",
                candidates[0]);
        }

        /// <summary>界面资源路径：优先 assets\，兼容旧版 bin\。</summary>
        internal static string AssetPath(string fileName)
        {
            string root = AppDir();
            string[] dirs = new string[] { "assets", "bin" };
            for (int i = 0; i < dirs.Length; i++)
            {
                string candidate = Path.Combine(root, dirs[i], fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return Path.Combine(root, "assets", fileName);
        }

        internal static ProcessStartInfo EngineStartInfo(string arguments)
        {
            string root = AppDir();
            string runtime = RuntimeDir();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = EnginePath();
            startInfo.Arguments = arguments ?? "";
            startInfo.WorkingDirectory = root;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            StringBuilder pathBuilder = new StringBuilder();
            string custom = CustomFfmpegDir;
            if (!string.IsNullOrEmpty(custom) &&
                File.Exists(Path.Combine(custom, "ffmpeg.exe")))
            {
                pathBuilder.Append(Path.GetFullPath(custom));
                pathBuilder.Append(Path.PathSeparator);
            }
            pathBuilder.Append(Path.GetFullPath(root));
            pathBuilder.Append(Path.PathSeparator);
            pathBuilder.Append(Path.GetFullPath(runtime));
            pathBuilder.Append(Path.PathSeparator);
            pathBuilder.Append(currentPath);
            startInfo.EnvironmentVariables["PATH"] = pathBuilder.ToString();
            startInfo.EnvironmentVariables["NM3U8DL_CLI_GUI"] = "1";
            startInfo.EnvironmentVariables["NM3U8DL_CLI_ROOT"] = root;
            return startInfo;
        }

        private static void EnsureFolderLayout()
        {
            try
            {
                string root = AppDir();
                Directory.CreateDirectory(Path.Combine(root, "Logs"));
                MoveEngineLogsToRoot();
                RemoveEmptyEngineLogDirectory();
            }
            catch
            {
            }
        }

        internal static void PrepareEngineLogDirectory()
        {
            string root = AppDir();
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            try
            {
                string engineDir = Path.GetDirectoryName(EnginePath());
                if (!string.IsNullOrEmpty(engineDir))
                {
                    Directory.CreateDirectory(Path.Combine(engineDir, "bin", "Logs"));
                }
            }
            catch
            {
            }
        }

        /// <summary>把引擎写在 runtime\ 或旧版 bin\ 下的日志搬到根目录 Logs。</summary>
        internal static void MoveEngineLogsToRoot()
        {
            lock (LogMoveLock)
            {
                string root = AppDir();
                string destination = Path.Combine(root, "Logs");
                try
                {
                    Directory.CreateDirectory(destination);
                }
                catch
                {
                    return;
                }

                string engineDir = RuntimeDir();
                string[] sources = new string[]
                {
                    Path.Combine(engineDir, "bin", "Logs"),
                    Path.Combine(engineDir, "Logs"),
                    Path.Combine(root, "bin", "Logs")
                };
                for (int i = 0; i < sources.Length; i++)
                {
                    MoveLogFiles(sources[i], destination);
                }
            }
        }

        private static void MoveLogFiles(string source, string destination)
        {
            try
            {
                if (!Directory.Exists(source))
                {
                    return;
                }
                string[] files = Directory.GetFiles(source);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        string target = Path.Combine(
                            destination,
                            Path.GetFileName(files[i]));
                        if (File.Exists(target))
                        {
                            target = Path.Combine(
                                destination,
                                Path.GetFileNameWithoutExtension(files[i]) +
                                "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") +
                                Path.GetExtension(files[i]));
                        }
                        File.Move(files[i], target);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            catch
            {
            }
        }

        internal static void RemoveEmptyEngineLogDirectory()
        {
            lock (LogMoveLock)
            {
                string root = AppDir();
                string engineDir = RuntimeDir();
                // 引擎会在自己目录下建 bin\Logs 和 Videos，搬空后一并收掉
                RemoveEmptyDirectory(Path.Combine(engineDir, "bin", "Logs"));
                RemoveEmptyDirectory(Path.Combine(engineDir, "bin"));
                RemoveEmptyDirectory(Path.Combine(engineDir, "Logs"));
                RemoveEmptyDirectory(Path.Combine(engineDir, "Videos"));
                RemoveEmptyDirectory(Path.Combine(root, "bin", "Logs"));
            }
        }

        private static void RemoveEmptyDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path) &&
                    Directory.GetFiles(path).Length == 0 &&
                    Directory.GetDirectories(path).Length == 0)
                {
                    Directory.Delete(path, false);
                }
            }
            catch
            {
            }
        }

        internal static string QuoteArguments(string[] args)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(Quote(args[i]));
            }
            return builder.ToString();
        }

        internal static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }
            if (value.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', '"' }) < 0)
            {
                return value;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int backslashes = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }
                if (backslashes > 0)
                {
                    builder.Append('\\', backslashes);
                    backslashes = 0;
                }
                builder.Append(c);
            }
            if (backslashes > 0)
            {
                builder.Append('\\', backslashes * 2);
            }
            builder.Append('"');
            return builder.ToString();
        }
    }

    internal sealed class DownloadJob
    {
        public string Url = "";
        public string SaveName = "";
        public string StorageName = "";
        public string WorkDir = "";
        public DateTime StartedAt = DateTime.Now;
        public Process Proc;
        public ListViewItem Item;
        public double Percent;
        public string SpeedText = "--";
        public string SizeText = "";
        public string Status = "等待";
        public string OutputFile = "";
        public bool UserStopped;
        public bool Finished;
        public bool InfiniteRetry;
        public bool ResetRetryOnReconnect;
        public int ReconnectAttempts;
        public bool WaitingForReconnect;
        public double ProgressAtReconnect;
        public System.Windows.Forms.Timer RetryTimer;

        public void SetStatus(string value)
        {
            Status = value;
            if (Item != null && Item.SubItems.Count > 3)
            {
                Item.SubItems[3].Text = Lang.Status(value);
            }
        }

        public void SetProgress(double value, string size)
        {
            Percent = Math.Max(
                Percent,
                Math.Max(0.0, Math.Min(100.0, value)));
            if (!string.IsNullOrEmpty(size))
            {
                SizeText = size;
            }
            if (Item != null && Item.SubItems.Count > 1)
            {
                Item.SubItems[1].Text = Percent.ToString("0.00") + "%";
            }
        }

        public void SetSpeed(string value)
        {
            SpeedText = value;
            if (Item != null && Item.SubItems.Count > 2)
            {
                Item.SubItems[2].Text = value;
            }
        }
    }

    internal sealed partial class MainForm : Form
    {
        private const int MaxReconnectAttempts = 5;
        private const string AppTitle = "m3u8DL-GUI";
        // 版本号唯一来源：bin\src\AssemblyInfo.cs 的 AssemblyVersion
        private static readonly string AppVersion =
            typeof(MainForm).Assembly.GetName().Version.ToString(3);
        private const string RepoUrl = "https://github.com/NILNILL/m3u8DL-GUI";

        // —— 数据 ——
        private readonly List<DownloadJob> jobs = new List<DownloadJob>();
        private readonly Dictionary<DownloadJob, ProgressLogLine> progressLogLines =
            new Dictionary<DownloadJob, ProgressLogLine>();
        private readonly string videosDir;
        private readonly string settingsPath;

        // —— 状态 ——
        private ThemeMode themeMode = ThemeMode.System;
        private AccentKind accentKind = AccentKind.FishBlue;
        private LangMode langMode = LangMode.System;
        private int ffmpegMode;          // 0 = 软件目录，1 = 自定义目录
        private string ffmpegDir = "";
        private int threadsValue = 16;
        private decimal speedValue;
        private bool alwaysOnTop;
        private bool deleteAfterDone = true;
        private bool continueAfterRetryLimit;
        private bool resetRetryOnReconnect;
        private bool currentDark;
        private bool initialized;
        private UiPalette palette;
        private System.Windows.Forms.Timer systemThemeTimer;
        private System.Windows.Forms.Timer pageTimer;
        private int pageTarget;
        private float pageT;
        private ToolTip tips;

        // —— 布局骨架 ——
        private Panel rail;
        private Panel headerBar;
        private Panel pageHost;
        private Panel downloadPage;
        private Panel settingsPage;
        private Panel settingsNav;
        private Panel settingsBody;
        private Panel themePane;
        private Panel aboutPane;
        private Panel statusBar;
        private SoftCard cardParams;
        private SoftCard cardTasks;
        private SoftCard cardProgress;
        private SoftCard cardAccent;
        private SoftCard cardMode;
        private SoftCard cardAbout;

        // —— 控件 ——
        private TextBox txtUrl;
        private TextBox txtName;
        private TextBox txtDir;
        private TextBox txtProxy;
        private SoftNumber numThreads;
        private SoftNumber numMaxSpeed;
        private SoftField fieldUrl;
        private SoftField fieldName;
        private SoftField fieldThread;
        private SoftField fieldSpeed;
        private SoftField fieldProxy;
        private SoftField fieldDir;
        private SoftField fieldLog;
        private SoftButton btnDownload;
        private SoftButton btnStop;
        private SoftButton btnRemove;
        private SoftButton btnClear;
        private SoftButton btnBrowse;
        private SoftButton btnOpen;
        private SoftIconButton btnSettings;
        private SoftCheckBox chkDelAfterDone;
        private SoftCheckBox chkInfiniteRetry;
        private SoftCheckBox chkResetRetryOnReconnect;
        private SoftCheckBox chkTopMost;
        private ListView lvTasks;
        private SoftBar progressBar;
        private Label lblSpeed;
        private Label lblProgress;
        private Label lblState;
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblVersion;
        private Label lblTasksTitle;
        private Label lblProgressTitle;
        private DotPanel stateBox;
        private RichTextBox txtLog;
        private PictureBox logo;
        private RailButton navDownload;
        private RailButton navSettings;
        private SideItem sideTheme;
        private SideItem sideAbout;
        private SegmentedSwitch segMode;
        private AccentSwatch swFish;
        private AccentSwatch swClaude;
        private Label lblThemeHeading;
        private Label lblThemeDesc;
        private Label lblModeHint;
        private Label lblAboutVersion;
        private Label lblAboutEngine;
        private LinkLabel lnkRepo;

        private sealed class ProgressLogLine
        {
            public int Start;
            public int Length;
        }

        private static readonly Regex RxPercent =
            new Regex("(\\d+(?:\\.\\d+)?)\\s*%", RegexOptions.Compiled);
        private static readonly Regex RxSpeed =
            new Regex("(\\d+(?:\\.\\d+)?\\s*[KMGTP]?B/s)(?:\\s*@\\s*([0-9A-Za-z:]+))?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxSizePair =
            new Regex("([\\d.]+\\s*[KMGTP]?B)/([\\d.]+\\s*[KMGTP]?B)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxName =
            new Regex("(?:文件名称|File\\s*Name)\\s*[:：]\\s*(.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxReconnect =
            new Regex("AUTO\\s*RETRY|自动重试|重试|重连",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public MainForm()
        {
            videosDir = Path.Combine(Program.AppDir(), "Videos");
            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "m3u8DL-GUI",
                "settings.ini");
            MigrateLegacySettings();
            try
            {
                Directory.CreateDirectory(videosDir);
            }
            catch
            {
            }

            LoadSettings();
            Lang.Setting = langMode;
            Lang.Current = Lang.Resolve(langMode);
            ApplyFfmpegSelection();
            BuildUi();
            TopMost = alwaysOnTop;
            txtDir.Text = videosDir;
            initialized = true;
            ApplyTheme();
            AppendLog("m3u8DL-GUI 已启动。");
            AppendLog("下载时使用软件目录内的 ffmpeg.exe。");
            AppendLog("视频保存在: " + videosDir);
            AppendLog("左侧齿轮可打开设置，切换主题色与外观模式。");
        }

        /// <summary>旧版设置文件（N_m3u8DL-CLI 目录）存在时，复制过来以便沿用。</summary>
        private void MigrateLegacySettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    return;
                }
                string legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "N_m3u8DL-CLI",
                    "settings.ini");
                if (!File.Exists(legacy))
                {
                    return;
                }
                string directory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Copy(legacy, settingsPath, false);
            }
            catch
            {
            }
        }

                private static string ActualText(TextBox textBox)
        {
            return textBox.Text.Trim();
        }

        private string CurrentVideosDir()
        {
            string path = txtDir.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                path = videosDir;
            }
            try
            {
                path = Environment.ExpandEnvironmentVariables(path);
                Directory.CreateDirectory(path);
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private void StartDownload()
        {
            string url = ActualText(txtUrl);
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(this, Lang.Translate("请输入下载链接。"), Lang.Translate("提示"),
                    MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                txtUrl.Focus();
                return;
            }
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(url) &&
                MessageBox.Show(this,
                    Lang.Translate("链接看起来不是 http(s) 地址，仍要尝试下载吗？"),
                    Lang.Translate("确认"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            DownloadJob job = new DownloadJob();
            job.Url = url;
            job.SaveName = txtName.Text.Trim();
            job.StorageName = job.SaveName;
            job.WorkDir = CurrentVideosDir();
            job.StartedAt = DateTime.Now;
            job.InfiniteRetry = chkInfiniteRetry.Checked;
            job.ResetRetryOnReconnect = chkResetRetryOnReconnect.Checked;
            job.Item = new ListViewItem(new string[]
            {
                string.IsNullOrEmpty(job.SaveName) ? "(自动命名)" : job.SaveName,
                "0.00%",
                "--",
                "启动中",
                url
            });
            lvTasks.Items.Add(job.Item);
            job.Item.Selected = true;
            job.Item.EnsureVisible();
            jobs.Add(job);
            txtUrl.Clear();
            txtName.Clear();

            AppendLog("------------------------------------------------------------");
            AppendLog("新建任务: " + url);
            UpdateProgressLog(job);
            try
            {
                StartWorker(job);
            }
            catch (Exception ex)
            {
                job.Finished = true;
                job.SetStatus("启动失败");
                AppendLog("[失败] 启动下载进程失败: " + ex.Message);
                MessageBox.Show(this,
                    Lang.Translate("启动下载进程失败:") + "\r\n" + ex.Message,
                    Lang.Translate("错误"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void StartWorker(DownloadJob job)
        {
            Program.PrepareEngineLogDirectory();
            string arguments = BuildArguments(job);
            ProcessStartInfo startInfo = Program.EngineStartInfo(arguments);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
            startInfo.StandardOutputEncoding = new UTF8Encoding(false);
            startInfo.StandardErrorEncoding = new UTF8Encoding(false);

            Process process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            if (!process.Start())
            {
                throw new InvalidOperationException("下载引擎未能启动。");
            }
            job.Proc = process;
            job.Finished = false;
            job.SetStatus("下载中");
            UpdateProgressLog(job);

            Thread outputThread = new Thread(delegate()
            {
                PumpStdout(job);
            });
            outputThread.IsBackground = true;
            outputThread.Start();

            Thread errorThread = new Thread(delegate()
            {
                PumpStderr(job);
            });
            errorThread.IsBackground = true;
            errorThread.Start();
        }

        private string BuildArguments(DownloadJob job)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Program.Quote(job.Url));
            builder.Append(" --workDir ");
            builder.Append(Program.Quote(job.WorkDir));
            if (!string.IsNullOrEmpty(job.SaveName))
            {
                builder.Append(" --saveName ");
                builder.Append(Program.Quote(job.SaveName));
            }
            string proxy = txtProxy.Text.Trim();
            if (!string.IsNullOrEmpty(proxy))
            {
                builder.Append(" --proxyAddress ");
                builder.Append(Program.Quote(proxy));
            }
            int threads = Convert.ToInt32(numThreads.Value);
            builder.Append(" --maxThreads ").Append(threads);
            builder.Append(" --minThreads ").Append(Math.Min(threads, 8));
            long maxSpeed = Convert.ToInt64(numMaxSpeed.Value);
            if (maxSpeed > 0)
            {
                builder.Append(" --maxSpeed ").Append(maxSpeed);
            }
            if (job.InfiniteRetry || job.ResetRetryOnReconnect)
            {
                builder.Append(" --retryCount 1");
            }
            if (chkDelAfterDone.Checked)
            {
                builder.Append(" --enableDelAfterDone");
            }
            return builder.ToString();
        }

        private void PumpStdout(DownloadJob job)
        {
            try
            {
                StreamReader reader = job.Proc.StandardOutput;
                StringBuilder lineBuilder = new StringBuilder();
                int value;
                while ((value = reader.Read()) >= 0)
                {
                    char c = (char)value;
                    if (c == '\r' || c == '\n')
                    {
                        if (lineBuilder.Length > 0)
                        {
                            string line = lineBuilder.ToString();
                            lineBuilder.Length = 0;
                            HandleLine(job, line);
                        }
                    }
                    else
                    {
                        lineBuilder.Append(c);
                        if (lineBuilder.Length > 4000)
                        {
                            lineBuilder.Length = 0;
                        }
                    }
                }
                if (lineBuilder.Length > 0)
                {
                    HandleLine(job, lineBuilder.ToString());
                }
            }
            catch
            {
            }

            int exitCode = -1;
            try
            {
                job.Proc.WaitForExit();
                exitCode = job.Proc.ExitCode;
            }
            catch
            {
            }
            Program.MoveEngineLogsToRoot();
            Program.RemoveEmptyEngineLogDirectory();

            string output = FindOutput(job);
            bool ok = !job.UserStopped &&
                exitCode == 0 &&
                !string.IsNullOrEmpty(output) &&
                File.Exists(output) &&
                new FileInfo(output).Length > 0;

            BeginInvokeSafe(delegate
            {
                HandleProcessExit(job, exitCode, ok, output);
            });
        }

        private void HandleProcessExit(
            DownloadJob job,
            int exitCode,
            bool ok,
            string output)
        {
            if (job.UserStopped)
            {
                CancelRetryTimer(job);
                job.SetStatus("已停止");
                job.Finished = true;
                UpdateProgressLog(job);
                RefreshSelection();
                return;
            }

            if (ok)
            {
                CancelRetryTimer(job);
                job.OutputFile = output;
                job.ReconnectAttempts = 0;
                job.WaitingForReconnect = false;
                job.SetProgress(100.0, job.SizeText);
                job.SetStatus("完成");
                job.Finished = true;
                UpdateProgressLog(job);
                AppendLog("[完成] " + job.Url + "  ->  " + output);
                RefreshSelection();
                return;
            }

            bool reconnected = job.ResetRetryOnReconnect &&
                job.WaitingForReconnect &&
                job.Percent > job.ProgressAtReconnect + 0.01;
            if (reconnected)
            {
                job.ReconnectAttempts = 0;
                job.WaitingForReconnect = false;
                AppendLog("[重连] 连接已恢复，重连次数已重新计数。");
            }

            int nextAttempt = job.ReconnectAttempts + 1;
            bool retry = job.InfiniteRetry ||
                (job.ResetRetryOnReconnect &&
                    nextAttempt <= MaxReconnectAttempts);
            if (retry)
            {
                job.ReconnectAttempts = nextAttempt;
                ScheduleReconnect(job, exitCode);
                return;
            }

            job.OutputFile = output;
            job.SetStatus("失败");
            job.Finished = true;
            UpdateProgressLog(job);
            if (exitCode == 0)
            {
                AppendLog("[失败] 未生成完整视频，分片可能不完整: " + job.Url);
            }
            else
            {
                AppendLog("[失败] " + job.Url + " (退出码 " + exitCode + ")");
            }
            RefreshSelection();
        }

        private void ScheduleReconnect(DownloadJob job, int exitCode)
        {
            CancelRetryTimer(job);
            job.SetStatus("重连中");
            UpdateProgressLog(job);
            string limitText = job.InfiniteRetry
                ? "不限次数"
                : "最多 " + MaxReconnectAttempts + " 次";
            AppendLog(
                "[重连] 第 " + job.ReconnectAttempts + " 次，进度 " +
                job.Percent.ToString("0.00") + "%，" + limitText +
                "；退出码 " + exitCode + "，3 秒后继续。");

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 3000;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                job.RetryTimer = null;
                if (job.UserStopped || job.Finished || !jobs.Contains(job))
                {
                    return;
                }
                try
                {
                    StartWorker(job);
                }
                catch (Exception ex)
                {
                    job.SetStatus("重连失败");
                    job.Finished = true;
                    AppendLog("[失败] 启动重连失败: " + ex.Message);
                    UpdateProgressLog(job);
                    RefreshSelection();
                }
            };
            job.RetryTimer = timer;
            timer.Start();
        }

        private static void CancelRetryTimer(DownloadJob job)
        {
            if (job == null || job.RetryTimer == null)
            {
                return;
            }
            try
            {
                job.RetryTimer.Stop();
                job.RetryTimer.Dispose();
            }
            catch
            {
            }
            job.RetryTimer = null;
        }

        private void PumpStderr(DownloadJob job)
        {
            try
            {
                StreamReader reader = job.Proc.StandardError;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string value = line.Trim();
                    if (value.Length > 0)
                    {
                        BeginInvokeSafe(delegate
                        {
                            AppendLog("[错误] " + value);
                        });
                    }
                }
            }
            catch
            {
            }
        }

        private void HandleLine(DownloadJob job, string raw)
        {
            string line = (raw ?? "").Trim();
            if (line.Length == 0)
            {
                return;
            }

            bool progressLine = line.StartsWith("Progress:") ||
                line.StartsWith("(");
            bool reconnectLine = RxReconnect.IsMatch(line);
            double? newPercent = null;
            string newSize = null;
            string newSpeed = null;

            Match percentMatch = RxPercent.Match(line);
            if (percentMatch.Success)
            {
                double percent;
                if (double.TryParse(percentMatch.Groups[1].Value, out percent))
                {
                    Match sizeMatch = RxSizePair.Match(line);
                    newPercent = percent;
                    newSize = sizeMatch.Success
                        ? sizeMatch.Groups[1].Value + "/" + sizeMatch.Groups[2].Value
                        : null;
                }
            }

            Match speedMatch = RxSpeed.Match(line);
            if (speedMatch.Success)
            {
                string speed = speedMatch.Groups[1].Value.Trim();
                if (speedMatch.Groups[2].Success &&
                    speedMatch.Groups[2].Value.Length > 0)
                {
                    speed += "  @ 剩余 " + speedMatch.Groups[2].Value;
                }
                newSpeed = speed;
            }

            Match nameMatch = RxName.Match(line);
            if (nameMatch.Success)
            {
                string name = nameMatch.Groups[1].Value.Trim();
                if (name.Length > 0)
                {
                    job.StorageName = name;
                    BeginInvokeSafe(delegate
                    {
                        if (job.Item != null)
                        {
                            job.Item.SubItems[0].Text = name;
                        }
                    });
                }
            }

            BeginInvokeSafe(delegate
            {
                if (newPercent.HasValue)
                {
                    job.SetProgress(newPercent.Value, newSize);
                }
                if (!string.IsNullOrEmpty(newSpeed))
                {
                    job.SetSpeed(newSpeed);
                }

                if (reconnectLine)
                {
                    job.WaitingForReconnect = true;
                    job.ProgressAtReconnect = job.Percent;
                    job.SetStatus("重连中");
                }

                if (job.ResetRetryOnReconnect &&
                    job.WaitingForReconnect &&
                    job.Percent > job.ProgressAtReconnect + 0.01)
                {
                    job.ReconnectAttempts = 0;
                    job.WaitingForReconnect = false;
                    job.SetStatus("下载中");
                    AppendLog("[重连] 连接已恢复，重连次数已重新计数。");
                }

                if (progressLine)
                {
                    if (job.Status == "重连中" && !reconnectLine)
                    {
                        job.SetStatus("下载中");
                    }
                    UpdateProgressLog(job);
                }
                else
                {
                    AppendLog(line);
                }
                RefreshSelection();
            });
        }

        private string FindOutput(DownloadJob job)
        {
            try
            {
                if (!Directory.Exists(job.WorkDir))
                {
                    return "";
                }
                string[] names = CandidateNames(job);
                string[] extensions = new string[]
                {
                    ".mp4", ".mkv", ".ts", ".flv", ".m4a", ".aac", ".srt"
                };
                FileInfo best = null;
                DateTime earliest = job.StartedAt.AddSeconds(-5);
                FileInfo[] files = new DirectoryInfo(job.WorkDir).GetFiles();
                for (int i = 0; i < files.Length; i++)
                {
                    FileInfo file = files[i];
                    string extension = file.Extension.ToLowerInvariant();
                    if (!extensions.Contains(extension) || file.Name.StartsWith("."))
                    {
                        continue;
                    }
                    if (file.LastWriteTime < earliest)
                    {
                        continue;
                    }
                    string baseName = Path.GetFileNameWithoutExtension(file.Name);
                    bool matches = names.Length == 0;
                    for (int j = 0; j < names.Length; j++)
                    {
                        if (FileBelongsToName(baseName, names[j]))
                        {
                            matches = true;
                            break;
                        }
                    }
                    if (matches && (best == null || file.LastWriteTime > best.LastWriteTime))
                    {
                        best = file;
                    }
                }
                return best == null ? "" : best.FullName;
            }
            catch
            {
                return "";
            }
        }

        private static bool FileBelongsToName(string baseName, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            if (string.Equals(baseName, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return baseName.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase) ||
                baseName.StartsWith(name + "(", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] CandidateNames(DownloadJob job)
        {
            List<string> names = new List<string>();
            AddName(names, job.StorageName);
            AddName(names, job.SaveName);
            if (!string.IsNullOrEmpty(job.OutputFile))
            {
                AddName(names, Path.GetFileNameWithoutExtension(job.OutputFile));
            }
            return names.ToArray();
        }

        private static void AddName(List<string> names, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            names.Add(value);
        }

        private DownloadJob SelectedJob()
        {
            if (lvTasks.SelectedItems.Count == 0)
            {
                return null;
            }
            ListViewItem item = lvTasks.SelectedItems[0];
            return jobs.FirstOrDefault(delegate(DownloadJob job)
            {
                return job.Item == item;
            });
        }

        private void RefreshSelection()
        {
            DownloadJob job = SelectedJob();
            if (job == null)
            {
                progressBar.Value = 0;
                lblSpeed.Text = Lang.Translate("速度") + ":  --";
                lblProgress.Text = Lang.Translate("进度") + ":  --";
                lblState.Text = Lang.Translate("状态") + ":  " + Lang.Translate("就绪");
                stateBox.BackColor = Color.Silver;
                return;
            }
            progressBar.Value = Convert.ToInt32(Math.Max(0.0, Math.Min(100.0, job.Percent)));
            lblSpeed.Text = Lang.Translate("速度") + ":  " + job.SpeedText;
            lblProgress.Text = Lang.Translate("进度") + ":  " + job.Percent.ToString("0.00") + "%" +
                (string.IsNullOrEmpty(job.SizeText) ? "" : "  (" + job.SizeText + ")");
            lblState.Text = Lang.Translate("状态") + ":  " + Lang.Status(job.Status);
            stateBox.BackColor = StateColor(job.Status);
        }

        private void StopSelected()
        {
            DownloadJob job = SelectedJob();
            if (job == null)
            {
                return;
            }
            CancelRetryTimer(job);
            StopProcessTree(job);
            job.UserStopped = true;
            job.SetStatus("已停止");
            AppendLog("[停止] " + job.Url);
            RefreshSelection();
        }

        private void RemoveSelected()
        {
            DownloadJob job = SelectedJob();
            if (job == null)
            {
                return;
            }

            CancelRetryTimer(job);
            List<string> targets = BuildDeleteTargets(job);
            bool running = job.Proc != null && !HasExited(job.Proc);
            string message = running
                ? Lang.Translate("任务正在下载，停止后会删除任务及对应文件。") + "\r\n\r\n"
                : Lang.Translate("将从任务列表删除，并删除以下下载内容：") + "\r\n\r\n";
            if (targets.Count == 0)
            {
                message += Lang.Translate("（当前没有检测到已生成的视频或分片文件）");
            }
            else
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    message += "• " + targets[i] + "\r\n";
                }
            }
            message += "\r\n" + Lang.Translate("删除后无法恢复，是否继续？");
            if (MessageBox.Show(this, message, Lang.Translate("删除任务和文件"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            if (running)
            {
                StopProcessTree(job);
                WaitForExit(job.Proc, 2000);
            }

            int deleted = DeleteOutputTargets(job, targets);

            lvTasks.Items.Remove(job.Item);
            jobs.Remove(job);
            AppendLog("[删除] 任务已删除，同时清理 " + deleted + " 个文件或目录。");
            RefreshSelection();
        }

        private int DeleteOutputTargets(DownloadJob job, List<string> targets)
        {
            int deleted = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                try
                {
                    string target = targets[i];
                    if (!IsSafeOutputPath(job.WorkDir, target))
                    {
                        AppendLog("[警告] 已拒绝删除工作目录外的路径: " + target);
                        continue;
                    }
                    if (Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                        deleted++;
                    }
                    else if (File.Exists(target))
                    {
                        File.Delete(target);
                        deleted++;
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("[失败] 删除 " + targets[i] + " 失败: " + ex.Message);
                }
            }
            return deleted;
        }

        private List<string> BuildDeleteTargets(DownloadJob job)
        {
            List<string> targets = new List<string>();
            string[] names = CandidateNames(job);
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                AddTarget(targets, Path.Combine(job.WorkDir, name));
                AddTarget(targets, Path.Combine(job.WorkDir, name + "(Audio)"));
                AddTarget(targets, Path.Combine(job.WorkDir, name + "(Subtitle)"));
            }
            if (!string.IsNullOrEmpty(job.OutputFile))
            {
                AddTarget(targets, job.OutputFile);
            }

            try
            {
                if (Directory.Exists(job.WorkDir))
                {
                    FileInfo[] files = new DirectoryInfo(job.WorkDir).GetFiles();
                    string[] extensions = new string[]
                    {
                        ".mp4", ".mkv", ".ts", ".flv", ".m4a", ".aac", ".srt"
                    };
                    for (int i = 0; i < files.Length; i++)
                    {
                        FileInfo file = files[i];
                        if (!extensions.Contains(file.Extension.ToLowerInvariant()))
                        {
                            continue;
                        }
                        string baseName = Path.GetFileNameWithoutExtension(file.Name);
                        for (int j = 0; j < names.Length; j++)
                        {
                            if (FileBelongsToName(baseName, names[j]))
                            {
                                AddTarget(targets, file.FullName);
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return targets;
        }

        private static void AddTarget(List<string> targets, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            string fullPath = Path.GetFullPath(path);
            if (!targets.Any(delegate(string item)
            {
                return string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase);
            }))
            {
                targets.Add(fullPath);
            }
        }

        private static bool IsSafeOutputPath(string workDir, string target)
        {
            try
            {
                string root = Path.GetFullPath(workDir)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(target);
                return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void StopProcessTree(DownloadJob job)
        {
            if (job == null || job.Proc == null || HasExited(job.Proc))
            {
                return;
            }
            try
            {
                ProcessStartInfo killInfo = new ProcessStartInfo();
                killInfo.FileName = "taskkill.exe";
                killInfo.Arguments = "/PID " + job.Proc.Id + " /T /F";
                killInfo.UseShellExecute = false;
                killInfo.CreateNoWindow = true;
                using (Process kill = Process.Start(killInfo))
                {
                    if (kill != null)
                    {
                        kill.WaitForExit(3000);
                    }
                }
            }
            catch
            {
                try
                {
                    job.Proc.Kill();
                }
                catch
                {
                }
            }
            job.UserStopped = true;
        }

        private static bool HasExited(Process process)
        {
            try
            {
                return process == null || process.HasExited;
            }
            catch
            {
                return true;
            }
        }

        private static void WaitForExit(Process process, int milliseconds)
        {
            try
            {
                if (process != null)
                {
                    process.WaitForExit(milliseconds);
                }
            }
            catch
            {
            }
        }

        private void ClearFinished()
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                DownloadJob job = jobs[i];
                if (job.Finished && !HasExited(job.Proc))
                {
                    continue;
                }
                if (job.Finished || job.Status == "失败" ||
                    job.Status == "已停止" || job.Status == "完成")
                {
                    lvTasks.Items.Remove(job.Item);
                    jobs.RemoveAt(i);
                }
            }
            RefreshSelection();
        }

        private void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    path = videosDir;
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "打开文件夹失败: " + ex.Message,
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BeginInvokeSafe(Action action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }
                if (InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch
            {
            }
        }

        private void UpdateProgressLog(DownloadJob job)
        {
            if (job == null || job.Item == null ||
                txtLog == null || txtLog.IsDisposed)
            {
                return;
            }
            try
            {
                string name = job.Item.SubItems[0].Text;
                if (string.IsNullOrEmpty(name))
                {
                    name = "(自动命名)";
                }
                string text = DateTime.Now.ToString("HH:mm:ss") +
                    "  ■ " + name +
                    " | " + job.Status +
                    " | " + job.Percent.ToString("0.00") + "%" +
                    (string.IsNullOrEmpty(job.SizeText)
                        ? ""
                        : " | " + job.SizeText) +
                    " | 速度 " + (job.SpeedText ?? "--") +
                    (job.ReconnectAttempts > 0
                        ? " | 重连 " + job.ReconnectAttempts + " 次"
                        : "") +
                    Environment.NewLine;

                ProgressLogLine entry;
                if (!progressLogLines.TryGetValue(job, out entry))
                {
                    if (txtLog.TextLength > 200000)
                    {
                        txtLog.Clear();
                        progressLogLines.Clear();
                    }
                    entry = new ProgressLogLine();
                    entry.Start = txtLog.TextLength;
                    entry.Length = text.Length;
                    progressLogLines[job] = entry;
                    txtLog.AppendText(text);
                }
                else
                {
                    int oldStart = entry.Start;
                    int oldLength = entry.Length;
                    txtLog.Select(oldStart, oldLength);
                    txtLog.SelectedText = text;
                    entry.Length = text.Length;
                    int delta = text.Length - oldLength;
                    if (delta != 0)
                    {
                        foreach (KeyValuePair<DownloadJob, ProgressLogLine> item
                            in progressLogLines)
                        {
                            if (item.Value != entry &&
                                item.Value.Start > oldStart)
                            {
                                item.Value.Start += delta;
                            }
                        }
                    }
                }

                Color color = StateColor(job.Status);
                txtLog.Select(entry.Start, entry.Length);
                txtLog.SelectionColor = color;
                txtLog.Select(txtLog.TextLength, 0);
            }
            catch
            {
            }
        }

        private void AppendLog(string line)
        {
            try
            {
                if (txtLog == null || txtLog.IsDisposed)
                {
                    return;
                }
                if (txtLog.TextLength > 200000)
                {
                    txtLog.Clear();
                    progressLogLines.Clear();
                }
                Color color = ClassifyColor(line);
                int start = txtLog.TextLength;
                string text = DateTime.Now.ToString("HH:mm:ss") +
                    "  ■ " + line + Environment.NewLine;
                txtLog.AppendText(text);
                txtLog.Select(start, text.Length);
                txtLog.SelectionColor = color;
                txtLog.Select(txtLog.TextLength, 0);
                txtLog.ScrollToCaret();
            }
            catch
            {
            }
        }

        private static Color StateColor(string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                return Color.Silver;
            }
            if (status.Contains("完成"))
            {
                return Color.FromArgb(70, 200, 120);
            }
            if (status.Contains("失败"))
            {
                return Color.FromArgb(240, 100, 100);
            }
            if (status.Contains("停止") || status.Contains("启动失败"))
            {
                return Color.FromArgb(240, 175, 60);
            }
            if (status.Contains("下载中"))
            {
                return Color.FromArgb(66, 133, 244);
            }
            return Color.Silver;
        }

        private Color ClassifyColor(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return InfoColor();
            }
            if (line.Contains("[错误]") || line.Contains("错误") ||
                line.Contains("[失败]") || line.Contains("失败") ||
                line.Contains("地址无效"))
            {
                return Color.FromArgb(240, 100, 100);
            }
            if (line.Contains("[停止]") || line.Contains("已停止") ||
                line.Contains("警告") || line.Contains("Warning"))
            {
                return Color.FromArgb(240, 175, 60);
            }
            if (line.Contains("[完成]") || line.Contains("任务结束") ||
                line.Contains("下载完毕"))
            {
                return Color.FromArgb(70, 200, 120);
            }
            return InfoColor();
        }

        private Color InfoColor()
        {
            return Color.FromArgb(200, 205, 212);
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("theme=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring(line.IndexOf('=') + 1).Trim();
                        if (string.Equals(value, "light", StringComparison.OrdinalIgnoreCase))
                        {
                            themeMode = ThemeMode.Light;
                        }
                        else if (string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase))
                        {
                            themeMode = ThemeMode.Dark;
                        }
                        else
                        {
                            themeMode = ThemeMode.System;
                        }
                    }
                    else if (line.StartsWith("accent=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring(line.IndexOf('=') + 1).Trim();
                        accentKind = string.Equals(
                            value, "claude", StringComparison.OrdinalIgnoreCase)
                            ? AccentKind.ClaudeOrange
                            : AccentKind.FishBlue;
                    }
                    else if (line.StartsWith("lang=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring(line.IndexOf('=') + 1).Trim();
                        if (string.Equals(value, "zh-hans", StringComparison.OrdinalIgnoreCase))
                        {
                            langMode = LangMode.ZhHans;
                        }
                        else if (string.Equals(value, "zh-hant", StringComparison.OrdinalIgnoreCase))
                        {
                            langMode = LangMode.ZhHant;
                        }
                        else if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase))
                        {
                            langMode = LangMode.En;
                        }
                        else
                        {
                            langMode = LangMode.System;
                        }
                    }
                    else if (line.StartsWith("threads=", StringComparison.OrdinalIgnoreCase))
                    {
                        int value;
                        if (int.TryParse(
                            line.Substring(line.IndexOf('=') + 1).Trim(),
                            out value) && value >= 1 && value <= 256)
                        {
                            threadsValue = value;
                        }
                    }
                    else if (line.StartsWith("max_speed=", StringComparison.OrdinalIgnoreCase))
                    {
                        decimal value;
                        if (decimal.TryParse(
                            line.Substring(line.IndexOf('=') + 1).Trim(),
                            out value) && value >= 0m)
                        {
                            speedValue = value;
                        }
                    }
                    else if (line.StartsWith("ffmpeg_mode=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring(line.IndexOf('=') + 1).Trim();
                        ffmpegMode = string.Equals(value, "custom", StringComparison.OrdinalIgnoreCase)
                            ? 1
                            : 0;
                    }
                    else if (line.StartsWith("ffmpeg_dir=", StringComparison.OrdinalIgnoreCase))
                    {
                        ffmpegDir = line.Substring(line.IndexOf('=') + 1).Trim();
                    }
                    else if (line.StartsWith("topmost=", StringComparison.OrdinalIgnoreCase))
                    {
                        alwaysOnTop = string.Equals(
                            line.Substring(line.IndexOf('=') + 1).Trim(),
                            "true",
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith(
                        "delete_after_done=",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        deleteAfterDone = string.Equals(
                            line.Substring(line.IndexOf('=') + 1).Trim(),
                            "true",
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith(
                        "continue_after_retry_limit=",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continueAfterRetryLimit = string.Equals(
                            line.Substring(line.IndexOf('=') + 1).Trim(),
                            "true",
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith(
                        "reset_retry_on_reconnect=",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        resetRetryOnReconnect = string.Equals(
                            line.Substring(line.IndexOf('=') + 1).Trim(),
                            "true",
                            StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
            }
        }

        private void SaveSettings()
        {
            try
            {
                string directory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string theme = themeMode == ThemeMode.Light
                    ? "light"
                    : themeMode == ThemeMode.Dark ? "dark" : "system";
                string accent = accentKind == AccentKind.ClaudeOrange
                    ? "claude"
                    : "fish";
                string lang = langMode == LangMode.ZhHans
                    ? "zh-hans"
                    : langMode == LangMode.ZhHant ? "zh-hant"
                    : langMode == LangMode.En ? "en" : "system";
                File.WriteAllText(
                    settingsPath,
                    "theme=" + theme + Environment.NewLine +
                    "accent=" + accent + Environment.NewLine +
                    "lang=" + lang + Environment.NewLine +
                    "ffmpeg_mode=" + (ffmpegMode == 1 ? "custom" : "bundled") +
                    Environment.NewLine +
                    "ffmpeg_dir=" + ffmpegDir + Environment.NewLine +
                    "threads=" + threadsValue.ToString() + Environment.NewLine +
                    "max_speed=" + speedValue.ToString("0") + Environment.NewLine +
                    "topmost=" + (alwaysOnTop ? "true" : "false") +
                    Environment.NewLine +
                    "delete_after_done=" +
                    (deleteAfterDone ? "true" : "false") +
                    Environment.NewLine +
                    "continue_after_retry_limit=" +
                    (continueAfterRetryLimit ? "true" : "false") +
                    Environment.NewLine +
                    "reset_retry_on_reconnect=" +
                    (resetRetryOnReconnect ? "true" : "false") +
                    Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
            }
        }

        /// <summary>把设置里的 ffmpeg 选择同步到 Program（引擎启动时读它）。</summary>
        private void ApplyFfmpegSelection()
        {
            if (ffmpegMode == 1 &&
                !string.IsNullOrEmpty(ffmpegDir) &&
                File.Exists(Path.Combine(ffmpegDir, "ffmpeg.exe")))
            {
                Program.CustomFfmpegDir = ffmpegDir;
            }
            else
            {
                Program.CustomFfmpegDir = "";
            }
        }

        private bool IsSystemDark()
        {
            try
            {
                object value = Registry.GetValue(
                    "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                    "AppsUseLightTheme",
                    null);
                return value is int && (int)value == 0;
            }
            catch
            {
                return false;
            }
        }

                protected override void OnFormClosing(FormClosingEventArgs e)
        {
            bool running = jobs.Any(delegate(DownloadJob job)
            {
                return (job.Proc != null && !HasExited(job.Proc)) ||
                    job.RetryTimer != null;
            });
            if (running)
            {
                DialogResult result = MessageBox.Show(
                    this,
                    Lang.Translate("仍有下载任务正在运行，确定要退出并停止全部任务吗？"),
                    Lang.Translate("确认退出"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation);
                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                for (int i = 0; i < jobs.Count; i++)
                {
                    CancelRetryTimer(jobs[i]);
                    StopProcessTree(jobs[i]);
                }
            }
            else
            {
                for (int i = 0; i < jobs.Count; i++)
                {
                    CancelRetryTimer(jobs[i]);
                }
            }
            Program.MoveEngineLogsToRoot();
            Program.RemoveEmptyEngineLogDirectory();
            if (systemThemeTimer != null)
            {
                systemThemeTimer.Stop();
                systemThemeTimer.Dispose();
            }
            base.OnFormClosing(e);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        internal static extern bool AttachConsole(int processId);

        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            ref int value,
            int valueSize);
    }
}
