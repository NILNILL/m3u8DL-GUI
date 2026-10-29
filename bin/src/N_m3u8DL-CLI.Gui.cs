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

namespace N_m3u8DL_Gui
{
    internal static class Program
    {
        private static readonly object LogMoveLock = new object();

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
                    "无法启动下载引擎：\r\n" + ex.Message,
                    "N_m3u8DL",
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

        internal static string EnginePath()
        {
            string root = AppDir();
            string engine = Path.Combine(root, "N_m3u8DL-CLI-engine.exe");
            if (File.Exists(engine))
            {
                return engine;
            }

            engine = Path.Combine(root, "bin", "N_m3u8DL-CLI-engine.exe");
            if (File.Exists(engine))
            {
                return engine;
            }

            throw new FileNotFoundException(
                "缺少下载引擎 N_m3u8DL-CLI-engine.exe，请保持软件目录完整。",
                engine);
        }

        internal static ProcessStartInfo EngineStartInfo(string arguments)
        {
            string root = AppDir();
            string supportDir = Path.Combine(root, "bin");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = EnginePath();
            startInfo.Arguments = arguments ?? "";
            startInfo.WorkingDirectory = root;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            string supportPath = Path.GetFullPath(supportDir);
            string rootPath = Path.GetFullPath(root);
            startInfo.EnvironmentVariables["PATH"] =
                rootPath + Path.PathSeparator +
                supportPath + Path.PathSeparator + currentPath;
            startInfo.EnvironmentVariables["NM3U8DL_CLI_GUI"] = "1";
            startInfo.EnvironmentVariables["NM3U8DL_CLI_ROOT"] = root;
            return startInfo;
        }

        private static void EnsureFolderLayout()
        {
            try
            {
                string root = AppDir();
                string support = Path.Combine(root, "bin");
                string logs = Path.Combine(root, "Logs");
                Directory.CreateDirectory(support);
                Directory.CreateDirectory(logs);
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
            Directory.CreateDirectory(Path.Combine(root, "bin", "Logs"));
        }

        internal static void MoveEngineLogsToRoot()
        {
            lock (LogMoveLock)
            {
                try
                {
                    string root = AppDir();
                    string source = Path.Combine(root, "bin", "Logs");
                    string destination = Path.Combine(root, "Logs");
                    if (!Directory.Exists(source))
                    {
                        return;
                    }
                    Directory.CreateDirectory(destination);
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
        }

        internal static void RemoveEmptyEngineLogDirectory()
        {
            lock (LogMoveLock)
            {
                try
                {
                    string path = Path.Combine(AppDir(), "bin", "Logs");
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
                Item.SubItems[3].Text = value;
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

    internal sealed class MainForm : Form
    {
        private enum ThemeMode
        {
            System,
            Light,
            Dark
        }

        private TextBox txtUrl;
        private TextBox txtName;
        private TextBox txtDir;
        private TextBox txtProxy;
        private NumericUpDown numThreads;
        private NumericUpDown numMaxSpeed;
        private Button btnDownload;
        private Button btnStop;
        private Button btnRemove;
        private Button btnClear;
        private Button btnBrowse;
        private Button btnOpen;
        private CheckBox chkDelAfterDone;
        private CheckBox chkInfiniteRetry;
        private CheckBox chkResetRetryOnReconnect;
        private ListView lvTasks;
        private ProgressBar progressBar;
        private Label lblSpeed;
        private Label lblProgress;
        private Label lblState;
        private Label lblTheme;
        private RichTextBox txtLog;
        private Panel stateBox;
        private Panel themeBar;
        private Panel statusBar;
        private TableLayoutPanel mainLayout;
        private ComboBox cboTheme;
        private System.Windows.Forms.Timer systemThemeTimer;
        private readonly Dictionary<DownloadJob, ProgressLogLine> progressLogLines =
            new Dictionary<DownloadJob, ProgressLogLine>();
        private const int MaxReconnectAttempts = 5;

        private readonly List<DownloadJob> jobs = new List<DownloadJob>();
        private readonly string videosDir;
        private readonly string settingsPath;
        private ThemeMode themeMode = ThemeMode.System;
        private bool currentDark;
        private bool initialized;
        private Color themeWindow;
        private Color themeSurface;
        private Color themeInput;
        private Color themeText;
        private Color themeMuted;
        private Color themeButton;
        private Color themeBorder;

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
                "N_m3u8DL-CLI",
                "settings.ini");
            try
            {
                Directory.CreateDirectory(videosDir);
            }
            catch
            {
            }

            LoadSettings();
            BuildUi();
            txtDir.Text = videosDir;
            initialized = true;
            ApplyTheme();
            AppendLog("N_m3u8DL 图形界面已启动。");
            AppendLog("下载时使用软件目录内的 ffmpeg.exe。");
            AppendLog("视频保存在: " + videosDir);
            AppendLog("左上角可选择“跟随系统 / 浅色模式 / 深色模式”。");
        }

        private void BuildUi()
        {
            Text = "N_m3u8DL 视频下载器";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(920, 760);
            MinimumSize = new Size(860, 624);
            Font = new Font("Microsoft YaHei UI", 9f);

            try
            {
                string iconPath = Path.Combine(Program.AppDir(), "bin", "logo_3Iv_icon.ico");
                if (File.Exists(iconPath))
                {
                    Icon = new Icon(iconPath);
                }
                else
                {
                    Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch
            {
            }

            mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 1;
            mainLayout.RowCount = 5;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 192f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            Controls.Add(mainLayout);

            mainLayout.Controls.Add(BuildThemeBar(), 0, 0);
            mainLayout.Controls.Add(BuildTopPanel(), 0, 1);
            mainLayout.Controls.Add(BuildTaskPanel(), 0, 2);
            mainLayout.Controls.Add(BuildBottomPanel(), 0, 3);
            mainLayout.Controls.Add(BuildStatusBar(), 0, 4);

            systemThemeTimer = new System.Windows.Forms.Timer();
            systemThemeTimer.Interval = 1500;
            systemThemeTimer.Tick += delegate
            {
                if (themeMode == ThemeMode.System)
                {
                    bool systemDark = IsSystemDark();
                    if (systemDark != currentDark)
                    {
                        ApplyTheme();
                    }
                }
            };
            systemThemeTimer.Start();
        }

        private Control BuildThemeBar()
        {
            themeBar = new Panel();
            themeBar.Dock = DockStyle.Fill;
            themeBar.Padding = new Padding(0);

            lblTheme = new Label();
            lblTheme.Text = "主题:";
            lblTheme.AutoSize = true;
            lblTheme.Location = new Point(12, 5);

            cboTheme = new ComboBox();
            cboTheme.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTheme.AccessibleName = "主题模式";
            cboTheme.Width = 112;
            cboTheme.Location = new Point(56, 1);
            cboTheme.Items.Add("跟随系统");
            cboTheme.Items.Add("浅色模式");
            cboTheme.Items.Add("深色模式");
            cboTheme.SelectedIndex = (int)themeMode;
            cboTheme.SelectedIndexChanged += delegate
            {
                if (!initialized)
                {
                    return;
                }
                themeMode = (ThemeMode)cboTheme.SelectedIndex;
                SaveSettings();
                ApplyTheme();
            };

            themeBar.Controls.Add(lblTheme);
            themeBar.Controls.Add(cboTheme);
            return themeBar;
        }

        private Control BuildTopPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(10);

            Label urlLabel = MakeLabel("下载链接:", 12, 18);
            txtUrl = new TextBox();
            txtUrl.AccessibleName = "下载链接";
            txtUrl.Location = new Point(92, 15);
            txtUrl.Width = 800;
            txtUrl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label nameLabel = MakeLabel("保存名称:", 12, 50);
            txtName = new TextBox();
            txtName.AccessibleName = "保存名称";
            txtName.Location = new Point(92, 47);
            txtName.Width = 160;

            Label threadLabel = MakeLabel("线程:", 270, 50);
            numThreads = new NumericUpDown();
            numThreads.AccessibleName = "线程";
            numThreads.Location = new Point(330, 47);
            numThreads.Width = 56;
            numThreads.Minimum = 1m;
            numThreads.Maximum = 256m;
            numThreads.Value = 16m;

            Label speedLabel = MakeLabel("限速KB/s:", 404, 50);
            numMaxSpeed = new NumericUpDown();
            numMaxSpeed.AccessibleName = "限速KB/s";
            numMaxSpeed.Location = new Point(488, 47);
            numMaxSpeed.Width = 70;
            numMaxSpeed.Minimum = 0m;
            numMaxSpeed.Maximum = 100000000m;
            numMaxSpeed.Value = 0m;

            Label proxyLabel = MakeLabel("代理:", 576, 50);
            txtProxy = new TextBox();
            txtProxy.AccessibleName = "代理";
            txtProxy.Location = new Point(614, 47);
            txtProxy.Width = 294;
            txtProxy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label dirLabel = MakeLabel("保存目录:", 12, 82);
            txtDir = new TextBox();
            txtDir.AccessibleName = "保存目录";
            txtDir.Location = new Point(92, 79);
            txtDir.Width = 640;
            txtDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            btnBrowse = MakeButton("浏览...", 748, 78, 70, 26);
            btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择视频保存文件夹";
                    if (Directory.Exists(txtDir.Text))
                    {
                        dialog.SelectedPath = txtDir.Text;
                    }
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        txtDir.Text = dialog.SelectedPath;
                    }
                }
            };

            btnOpen = MakeButton("打开", 824, 78, 68, 26);
            btnOpen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpen.Click += delegate
            {
                OpenFolder(CurrentVideosDir());
            };

            btnDownload = MakeButton("开始下载", 92, 116, 120, 34);
            btnDownload.AccessibleName = "开始下载";
            btnDownload.Click += delegate
            {
                StartDownload();
            };

            btnStop = MakeButton("停止选中", 220, 116, 100, 34);
            btnStop.AccessibleName = "停止选中";
            btnStop.Click += delegate
            {
                StopSelected();
            };

            btnRemove = MakeButton("删除选中", 328, 116, 100, 34);
            btnRemove.AccessibleName = "删除选中";
            btnRemove.Click += delegate
            {
                RemoveSelected();
            };

            btnClear = MakeButton("清空已完成", 436, 116, 110, 34);
            btnClear.AccessibleName = "清空已完成";
            btnClear.Click += delegate
            {
                ClearFinished();
            };

            chkDelAfterDone = new CheckBox();
            chkDelAfterDone.AccessibleName = "完成后删除临时分片文件";
            chkDelAfterDone.Text = "完成后删除临时分片文件";
            chkDelAfterDone.Location = new Point(560, 122);
            chkDelAfterDone.AutoSize = true;
            chkDelAfterDone.Checked = true;

            chkInfiniteRetry = new CheckBox();
            chkInfiniteRetry.AccessibleName = "达到重连上限后继续下载";
            chkInfiniteRetry.Text = "达到重连上限后继续下载";
            chkInfiniteRetry.Location = new Point(92, 158);
            chkInfiniteRetry.AutoSize = true;

            chkResetRetryOnReconnect = new CheckBox();
            chkResetRetryOnReconnect.AccessibleName = "重连成功后重置次数";
            chkResetRetryOnReconnect.Text = "重连成功后重置次数";
            chkResetRetryOnReconnect.Location = new Point(365, 158);
            chkResetRetryOnReconnect.AutoSize = true;

            panel.Controls.Add(urlLabel);
            panel.Controls.Add(txtUrl);
            panel.Controls.Add(nameLabel);
            panel.Controls.Add(txtName);
            panel.Controls.Add(threadLabel);
            panel.Controls.Add(numThreads);
            panel.Controls.Add(speedLabel);
            panel.Controls.Add(numMaxSpeed);
            panel.Controls.Add(proxyLabel);
            panel.Controls.Add(txtProxy);
            panel.Controls.Add(dirLabel);
            panel.Controls.Add(txtDir);
            panel.Controls.Add(btnBrowse);
            panel.Controls.Add(btnOpen);
            panel.Controls.Add(btnDownload);
            panel.Controls.Add(btnStop);
            panel.Controls.Add(btnRemove);
            panel.Controls.Add(btnClear);
            panel.Controls.Add(chkDelAfterDone);
            panel.Controls.Add(chkInfiniteRetry);
            panel.Controls.Add(chkResetRetryOnReconnect);
            panel.Resize += delegate
            {
                LayoutTop(panel);
            };
            LayoutTop(panel);
            return panel;
        }

        private void LayoutTop(Panel panel)
        {
            int width = panel.ClientSize.Width;
            txtUrl.Width = Math.Max(80, width - 92 - 12);
            txtProxy.Width = Math.Max(80, width - 614 - 12);
            txtDir.Width = Math.Max(80, width - 92 - 12 - 80 - 80);
            btnBrowse.Left = width - 12 - 80 - 68 - 4;
            btnOpen.Left = width - 12 - 68;
        }

        private Control BuildTaskPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(10, 0, 10, 0);

            lvTasks = new ListView();
            lvTasks.AccessibleName = "下载任务";
            lvTasks.Dock = DockStyle.Fill;
            lvTasks.View = View.Details;
            lvTasks.OwnerDraw = true;
            lvTasks.FullRowSelect = true;
            lvTasks.GridLines = true;
            lvTasks.HideSelection = false;
            lvTasks.MultiSelect = false;
            lvTasks.DrawColumnHeader += DrawTaskColumnHeader;
            lvTasks.DrawItem += DrawTaskItem;
            lvTasks.DrawSubItem += DrawTaskSubItem;
            lvTasks.Columns.Add("名称", 300);
            lvTasks.Columns.Add("进度", 90);
            lvTasks.Columns.Add("速度", 130);
            lvTasks.Columns.Add("状态", 110);
            lvTasks.Columns.Add("地址", 240);
            lvTasks.Resize += delegate
            {
                int fixedWidth = 300 + 90 + 130 + 110;
                lvTasks.Columns[4].Width =
                    Math.Max(120, lvTasks.ClientSize.Width - fixedWidth - 2);
            };
            lvTasks.SelectedIndexChanged += delegate
            {
                RefreshSelection();
            };
            lvTasks.DoubleClick += delegate
            {
                DownloadJob job = SelectedJob();
                if (job != null)
                {
                    OpenFolder(job.WorkDir);
                }
            };
            panel.Controls.Add(lvTasks);
            return panel;
        }

        private void DrawTaskColumnHeader(
            object sender,
            DrawListViewColumnHeaderEventArgs e)
        {
            Color background = currentDark
                ? Color.FromArgb(56, 60, 65)
                : Color.FromArgb(243, 245, 247);
            Color foreground = currentDark
                ? Color.FromArgb(205, 209, 214)
                : Color.FromArgb(55, 59, 65);
            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            Rectangle textBounds = Rectangle.Inflate(e.Bounds, -6, 0);
            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                e.Font,
                textBounds,
                foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
            using (Pen pen = new Pen(themeBorder))
            {
                e.Graphics.DrawLine(
                    pen,
                    e.Bounds.Right - 1,
                    e.Bounds.Top,
                    e.Bounds.Right - 1,
                    e.Bounds.Bottom - 1);
            }
        }

        private void DrawTaskItem(object sender, DrawListViewItemEventArgs e)
        {
            if (!currentDark)
            {
                e.DrawDefault = true;
                return;
            }
            Color background = e.Item.Selected
                ? Color.FromArgb(67, 77, 88)
                : themeSurface;
            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
        }

        private void DrawTaskSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (!currentDark)
            {
                e.DrawDefault = true;
                return;
            }
            Color background = e.Item.Selected
                ? Color.FromArgb(67, 77, 88)
                : themeSurface;
            Color foreground = e.Item.Selected
                ? Color.FromArgb(229, 232, 235)
                : themeText;
            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            Rectangle textBounds = Rectangle.Inflate(e.Bounds, -6, 0);
            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                e.SubItem.Font,
                textBounds,
                foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
            using (Pen pen = new Pen(themeBorder))
            {
                e.Graphics.DrawLine(
                    pen,
                    e.Bounds.Right - 1,
                    e.Bounds.Top,
                    e.Bounds.Right - 1,
                    e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(
                    pen,
                    e.Bounds.Left,
                    e.Bounds.Bottom - 1,
                    e.Bounds.Right - 1,
                    e.Bounds.Bottom - 1);
            }
        }

        private Control BuildBottomPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(10);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(12, 10);
            progressBar.Width = 876;
            progressBar.Height = 18;
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            lblSpeed = new Label();
            lblSpeed.Text = "速度:  --";
            lblSpeed.AutoSize = false;
            lblSpeed.AutoEllipsis = true;
            lblSpeed.TextAlign = ContentAlignment.MiddleLeft;
            lblSpeed.Location = new Point(12, 34);
            lblSpeed.Size = new Size(240, 20);
            lblSpeed.Font = new Font(Font, FontStyle.Bold);

            lblProgress = new Label();
            lblProgress.Text = "进度:  --";
            lblProgress.AutoSize = false;
            lblProgress.AutoEllipsis = true;
            lblProgress.TextAlign = ContentAlignment.MiddleLeft;
            lblProgress.Location = new Point(264, 34);
            lblProgress.Size = new Size(250, 20);

            stateBox = new Panel();
            stateBox.Location = new Point(526, 37);
            stateBox.Size = new Size(12, 12);
            stateBox.Tag = "color-swatch";

            lblState = new Label();
            lblState.Text = "状态:  就绪";
            lblState.AutoSize = false;
            lblState.AutoEllipsis = true;
            lblState.TextAlign = ContentAlignment.MiddleLeft;
            lblState.Location = new Point(544, 34);
            lblState.Size = new Size(344, 20);

            int legendX = 12;
            legendX = AddLogLegend(
                panel, legendX, 62, Color.FromArgb(200, 205, 212), "信息");
            legendX = AddLogLegend(
                panel, legendX, 62, Color.FromArgb(70, 200, 120), "成功");
            legendX = AddLogLegend(
                panel, legendX, 62, Color.FromArgb(240, 175, 60), "警告");
            AddLogLegend(
                panel, legendX, 62, Color.FromArgb(240, 100, 100), "失败");

            txtLog = new RichTextBox();
            txtLog.Location = new Point(12, 86);
            txtLog.Width = 876;
            txtLog.Height = 158;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtLog.WordWrap = false;
            txtLog.BorderStyle = BorderStyle.FixedSingle;
            txtLog.Font = new Font("Consolas", 9f);
            txtLog.DetectUrls = false;
            txtLog.BackColor = Color.FromArgb(28, 30, 34);
            txtLog.ForeColor = Color.Gainsboro;
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                AnchorStyles.Left | AnchorStyles.Right;

            panel.Controls.Add(progressBar);
            panel.Controls.Add(lblSpeed);
            panel.Controls.Add(lblProgress);
            panel.Controls.Add(stateBox);
            panel.Controls.Add(lblState);
            panel.Controls.Add(txtLog);
            panel.Resize += delegate
            {
                LayoutBottomInfo(panel);
            };
            LayoutBottomInfo(panel);
            return panel;
        }

        private void LayoutBottomInfo(Panel panel)
        {
            int width = panel.ClientSize.Width;
            int height = panel.ClientSize.Height;
            int available = Math.Max(200, width - 24);
            int speedWidth = Math.Max(280, (int)(available * 0.34));
            int progressWidth = Math.Max(300, (int)(available * 0.38));

            progressBar.Width = available;
            lblSpeed.SetBounds(12, 34, speedWidth, 20);
            lblProgress.SetBounds(
                12 + speedWidth + 16,
                34,
                progressWidth,
                20);
            int stateLeft = 12 + speedWidth + progressWidth + 36;
            stateBox.Location = new Point(stateLeft, 37);
            lblState.SetBounds(
                stateLeft + 20,
                34,
                Math.Max(80, width - stateLeft - 32),
                20);
            txtLog.Width = available;
            txtLog.Height = Math.Max(40, height - 98);
        }

        private static int AddLogLegend(
            Panel host,
            int left,
            int top,
            Color color,
            string text)
        {
            Panel marker = new Panel();
            marker.Location = new Point(left, top + 2);
            marker.Size = new Size(12, 12);
            marker.BackColor = color;
            marker.Tag = "color-swatch";

            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(left + 17, top - 1);

            host.Controls.Add(marker);
            host.Controls.Add(label);
            return left + 17 + TextRenderer.MeasureText(text, host.Font).Width + 18;
        }

        private Control BuildStatusBar()
        {
            statusBar = new Panel();
            statusBar.Dock = DockStyle.Fill;

            Panel line = new Panel();
            line.Dock = DockStyle.Top;
            line.Height = 1;
            line.Tag = "status-line";

            Label developer = new Label();
            developer.Text = "开发者:NIL";
            developer.AutoSize = true;
            developer.Location = new Point(12, 7);

            Label version = new Label();
            version.Text = "version:1.1.0";
            version.AutoSize = true;
            version.Location = new Point(850, 7);

            statusBar.Controls.Add(line);
            statusBar.Controls.Add(developer);
            statusBar.Controls.Add(version);
            statusBar.Resize += delegate
            {
                version.Left = Math.Max(12, statusBar.ClientSize.Width - version.Width - 12);
            };
            return statusBar;
        }

        private Label MakeLabel(string text, int left, int top)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(left, top);
            return label;
        }

        private Button MakeButton(string text, int left, int top, int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(left, top);
            button.Size = new Size(width, height);
            return button;
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
                MessageBox.Show(this, "请输入下载链接。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                txtUrl.Focus();
                return;
            }
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(url) &&
                MessageBox.Show(this,
                    "链接看起来不是 http(s) 地址，仍要尝试下载吗？",
                    "确认",
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
                    "启动下载进程失败:\r\n" + ex.Message,
                    "错误",
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
                lblSpeed.Text = "速度:  --";
                lblProgress.Text = "进度:  --";
                lblState.Text = "状态:  就绪";
                stateBox.BackColor = Color.Silver;
                return;
            }
            progressBar.Value = Convert.ToInt32(Math.Max(0.0, Math.Min(100.0, job.Percent)));
            lblSpeed.Text = "速度:  " + job.SpeedText;
            lblProgress.Text = "进度:  " + job.Percent.ToString("0.00") + "%" +
                (string.IsNullOrEmpty(job.SizeText) ? "" : "  (" + job.SizeText + ")");
            lblState.Text = "状态:  " + job.Status;
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
                ? "任务正在下载，停止后会删除任务及对应文件。\r\n\r\n"
                : "将从任务列表删除，并删除以下下载内容：\r\n\r\n";
            if (targets.Count == 0)
            {
                message += "（当前没有检测到已生成的视频或分片文件）";
            }
            else
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    message += "• " + targets[i] + "\r\n";
                }
            }
            message += "\r\n删除后无法恢复，是否继续？";
            if (MessageBox.Show(this, message, "删除任务和文件",
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
                        string value = line.Substring(6).Trim();
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
                Directory.CreateDirectory(directory);
                string value = themeMode == ThemeMode.Light
                    ? "light"
                    : themeMode == ThemeMode.Dark ? "dark" : "system";
                File.WriteAllText(settingsPath, "theme=" + value + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
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

        private void ApplyTheme()
        {
            currentDark = themeMode == ThemeMode.Dark ||
                (themeMode == ThemeMode.System && IsSystemDark());

            Color window = currentDark
                ? Color.FromArgb(43, 46, 50)
                : Color.FromArgb(245, 246, 248);
            Color surface = currentDark
                ? Color.FromArgb(48, 52, 56)
                : Color.White;
            Color input = currentDark
                ? Color.FromArgb(52, 56, 61)
                : Color.White;
            Color text = currentDark
                ? Color.FromArgb(212, 215, 219)
                : Color.FromArgb(34, 38, 44);
            Color muted = currentDark
                ? Color.FromArgb(174, 179, 185)
                : Color.FromArgb(80, 85, 92);
            Color button = currentDark
                ? Color.FromArgb(58, 63, 69)
                : SystemColors.Control;
            Color border = currentDark
                ? Color.FromArgb(74, 79, 86)
                : Color.FromArgb(198, 204, 212);
            themeWindow = window;
            themeSurface = surface;
            themeInput = input;
            themeText = text;
            themeMuted = muted;
            themeButton = button;
            themeBorder = border;

            BackColor = window;
            mainLayout.BackColor = window;
            ApplyThemeToControls(this, window, surface, input, text, muted, button, border);

            themeBar.BackColor = currentDark
                ? Color.FromArgb(42, 45, 49)
                : Color.FromArgb(235, 238, 242);
            statusBar.BackColor = currentDark
                ? Color.FromArgb(41, 44, 48)
                : Color.FromArgb(232, 235, 239);
            lblTheme.ForeColor = muted;
            cboTheme.BackColor = input;
            cboTheme.ForeColor = text;
            lblSpeed.ForeColor = currentDark
                ? Color.FromArgb(128, 190, 151)
                : Color.FromArgb(0, 120, 60);
            txtLog.BackColor = Color.FromArgb(28, 30, 34);
            txtLog.ForeColor = Color.Gainsboro;
            btnDownload.BackColor = currentDark
                ? Color.FromArgb(79, 128, 190)
                : Color.FromArgb(51, 122, 224);
            btnDownload.ForeColor = Color.White;
            btnDownload.FlatStyle = FlatStyle.Flat;
            btnDownload.FlatAppearance.BorderSize = 0;
            stateBox.BackColor = Color.Silver;
            RefreshSelection();
            ApplyNativeTitleBar();
        }

        private static void ApplyThemeToControls(
            Control parent,
            Color window,
            Color surface,
            Color input,
            Color text,
            Color muted,
            Color button,
            Color border)
        {
            for (int i = 0; i < parent.Controls.Count; i++)
            {
                Control control = parent.Controls[i];
                string tag = control.Tag as string;
                if (tag == "color-swatch")
                {
                    continue;
                }

                if (control is Panel || control is TableLayoutPanel)
                {
                    control.BackColor = window;
                }
                else if (control is TextBox || control is NumericUpDown ||
                    control is ComboBox)
                {
                    control.BackColor = input;
                    control.ForeColor =
                        control is TextBox && object.Equals(control.Text, control.Tag)
                        ? muted
                        : text;
                }
                else if (control is ListView)
                {
                    control.BackColor = surface;
                    control.ForeColor = text;
                }
                else if (control is Button)
                {
                    control.BackColor = button;
                    control.ForeColor = text;
                    ((Button)control).FlatAppearance.BorderColor = border;
                }
                else if (control is CheckBox)
                {
                    control.BackColor = window;
                    control.ForeColor = text;
                }
                else if (control is Label)
                {
                    control.BackColor = Color.Transparent;
                    control.ForeColor = muted;
                }
                else if (control is ProgressBar)
                {
                    control.BackColor = surface;
                    control.ForeColor = Color.FromArgb(51, 122, 224);
                }

                if (tag == "status-line")
                {
                    control.BackColor = border;
                }
                ApplyThemeToControls(control, window, surface, input, text,
                    muted, button, border);
            }
        }

        private void ApplyNativeTitleBar()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            try
            {
                int value = currentDark ? 1 : 0;
                int result = NativeMethods.DwmSetWindowAttribute(
                    Handle, 20, ref value, Marshal.SizeOf(typeof(int)));
                if (result != 0)
                {
                    NativeMethods.DwmSetWindowAttribute(
                        Handle, 19, ref value, Marshal.SizeOf(typeof(int)));
                }
            }
            catch
            {
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTheme();
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
                    "仍有下载任务正在运行，确定要退出并停止全部任务吗？",
                    "确认退出",
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
