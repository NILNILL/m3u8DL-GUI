using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace M3u8DlGui
{
    internal sealed partial class MainForm
    {
        // —— UI 专属字段 ——
        private Panel ffmpegPane;
        private Panel langPane;
        private SoftCard cardFfmpeg;
        private SoftCard cardLang;
        private SegmentedSwitch ffmpegSwitch;
        private SoftField fieldFfmpegDir;
        private TextBox txtFfmpegDir;
        private SoftButton btnFfmpegPick;
        private SoftButton btnFfmpegAuto;
        private Label lblFfmpegStatus;
        private SideItem sideFfmpeg;
        private SideItem sideLang;
        private SideItem[] langRows;
        private SoftButton btnCheckUpdate;
        private SoftButton btnOpenRelease;
        private Label lblUpdateStatus;
        private string releaseUrl = "";

        // 语言切换：记录每个控件的原始中文，切换时统一翻译
        private readonly Dictionary<Control, string> originalTexts =
            new Dictionary<Control, string>();
        private readonly string[] segModeLabels =
            new string[] { "浅色", "深色", "跟随系统" };
        private readonly string[] ffmpegLabels =
            new string[] { "使用软件目录", "自定义目录" };
        private readonly List<DotPanel> legendDots = new List<DotPanel>();
        private readonly List<Label> legendLabels = new List<Label>();
        private int legendTops = 88;
        private readonly string[] columnLabels =
            new string[] { "名称", "进度", "速度", "状态", "地址" };

        private TableLayoutPanel gridRoot;
        private TableLayoutPanel gridContent;
        private TableLayoutPanel gridPage;
        private TableLayoutPanel gridSettings;
        private Panel contentPane;
        private Label lblDeveloper;
        private Label lblUrlCaption;
        private Label lblNameCaption;
        private Label lblThreadCaption;
        private Label lblSpeedCaption;
        private Label lblProxyCaption;
        private Label lblDirCaption;
        private Font fontTitle;
        private Font fontHeading;
        private Font fontSection;
        private Font fontBody;
        private Font fontSmall;
        private DateTime pageStarted;
        private float pageStartT;
        private const int PageDurationMs = 220;

        /// <summary>
        /// WS_EX_COMPOSITED：让整窗自下而上一次性合成。
        /// 自绘控件与子窗口较多时，否则会出现子控件互相覆盖的重影。
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        private static int S(int value)
        {
            return UiMetrics.S(value);
        }

        private static Padding Pad(int left, int top, int right, int bottom)
        {
            return UiMetrics.P(left, top, right, bottom);
        }

        private void BuildUi()
        {
            try
            {
                using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero))
                {
                    UiMetrics.Scale = screen.DpiX / 96f;
                }
            }
            catch
            {
                UiMetrics.Scale = 1f;
            }

            Text = AppTitle;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = UiMetrics.SizeOf(1000, 812);
            MinimumSize = UiMetrics.SizeOf(940, 764);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(243, 245, 249);

            fontTitle = new Font("Microsoft YaHei UI", 15.5f, FontStyle.Bold);
            fontHeading = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
            fontSection = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            fontBody = new Font("Microsoft YaHei UI", 9.5f);
            fontSmall = new Font("Microsoft YaHei UI", 8.25f);

            try
            {
                string iconPath = Program.AssetPath("m3u8dl-gui.ico");
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

            tips = new ToolTip();
            tips.AutoPopDelay = 6000;
            tips.InitialDelay = 380;
            tips.ReshowDelay = 120;
            tips.ShowAlways = true;

            gridRoot = new TableLayoutPanel();
            gridRoot.Dock = DockStyle.Fill;
            gridRoot.ColumnCount = 2;
            gridRoot.RowCount = 1;
            gridRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(78)));
            gridRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            gridRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            gridRoot.Margin = new Padding(0);
            gridRoot.Padding = new Padding(0);
            Controls.Add(gridRoot);

            rail = BuildRail();
            rail.Margin = new Padding(0);

            contentPane = new Panel();
            contentPane.Dock = DockStyle.Fill;
            contentPane.Margin = new Padding(0);
            contentPane.Tag = "bg";

            gridContent = new TableLayoutPanel();
            gridContent.Dock = DockStyle.Fill;
            gridContent.ColumnCount = 1;
            gridContent.RowCount = 3;
            gridContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            gridContent.RowStyles.Add(new RowStyle(SizeType.Absolute, S(66)));
            gridContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            gridContent.RowStyles.Add(new RowStyle(SizeType.Absolute, S(30)));
            gridContent.Margin = new Padding(0);
            gridContent.Padding = new Padding(0);
            contentPane.Controls.Add(gridContent);

            headerBar = BuildHeader();
            pageHost = new Panel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.Margin = new Padding(0);
            pageHost.Tag = "bg";
            statusBar = BuildStatusBar();

            gridContent.Controls.Add(headerBar, 0, 0);
            gridContent.Controls.Add(pageHost, 0, 1);
            gridContent.Controls.Add(statusBar, 0, 2);

            downloadPage = BuildDownloadPage();
            settingsPage = BuildSettingsPage();
            downloadPage.Tag = "bg";
            settingsPage.Tag = "bg";
            pageHost.Controls.Add(downloadPage);
            pageHost.Controls.Add(settingsPage);
            pageHost.Resize += delegate
            {
                LayoutPages();
            };
            LayoutPages();

            gridRoot.Controls.Add(rail, 0, 0);
            gridRoot.Controls.Add(contentPane, 1, 0);

            navDownload.Active = true;

            Shown += delegate
            {
                // 句柄就绪后再设一次，确保深色标题栏在 Win10/11 生效
                ApplyNativeTitleBar();
            };

            systemThemeTimer = new System.Windows.Forms.Timer();
            systemThemeTimer.Interval = 1500;
            systemThemeTimer.Tick += delegate
            {
                if (themeMode != ThemeMode.System)
                {
                    return;
                }
                bool systemDark = IsSystemDark();
                if (systemDark != currentDark)
                {
                    ApplyTheme();
                }
            };
            systemThemeTimer.Start();

            CaptureOriginalTexts(this);
            ApplyLanguage();
        }

        // ————————————————————————————— 左侧导航 —————————————————————————————

        private Panel BuildRail()
        {
            Panel rp = new Panel();
            rp.Dock = DockStyle.Fill;
            rp.Tag = "rail";
            rp.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (palette == null)
                {
                    return;
                }
                using (Pen pen = new Pen(palette.Border))
                {
                    e.Graphics.DrawLine(
                        pen, rp.Width - 1, 0, rp.Width - 1, rp.Height);
                }
            };

            logo = new PictureBox();
            logo.SetBounds(S(23), S(22), S(32), S(32));
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.FromArgb(243, 245, 249);
            try
            {
                string logoPath = Program.AssetPath("m3u8dl-gui.png");
                if (File.Exists(logoPath))
                {
                    using (Image image = Image.FromFile(logoPath))
                    {
                        logo.Image = new Bitmap(image);
                    }
                }
            }
            catch
            {
            }
            rp.Controls.Add(logo);

            navDownload = new RailButton();
            navDownload.Kind = SoftIconKind.Download;
            navDownload.SetBounds(S(16), S(96), S(46), S(46));
            navDownload.AccessibleName = "下载页";
            navDownload.Click += delegate
            {
                ShowPage(0);
            };
            tips.SetToolTip(navDownload, "下载");

            navSettings = new RailButton();
            navSettings.Kind = SoftIconKind.Settings;
            navSettings.SetBounds(S(16), S(152), S(46), S(46));
            navSettings.AccessibleName = "设置页";
            navSettings.Click += delegate
            {
                ShowPage(1);
            };
            tips.SetToolTip(navSettings, "设置");

            rp.Controls.Add(navDownload);
            rp.Controls.Add(navSettings);
            return rp;
        }

        // ————————————————————————————— 顶栏 —————————————————————————————

        private Panel BuildHeader()
        {
            Panel hp = new Panel();
            hp.Dock = DockStyle.Fill;
            hp.Margin = new Padding(0);
            hp.Tag = "bg";
            hp.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (palette == null)
                {
                    return;
                }
                using (Pen pen = new Pen(palette.Hairline))
                {
                    e.Graphics.DrawLine(
                        pen, S(24), hp.Height - 1, hp.Width - S(24), hp.Height - 1);
                }
            };

            lblTitle = new Label();
            lblTitle.Text = AppTitle;
            lblTitle.Font = fontTitle;
            lblTitle.AutoSize = true;
            lblTitle.Location = UiMetrics.At(26, 8);
            lblTitle.Tag = "heading";
            lblTitle.BackColor = Color.FromArgb(243, 245, 249);

            lblSubtitle = new Label();
            lblSubtitle.Text = "视频下载器 · 支持 m3u8 / MPD / 直链";
            lblSubtitle.Font = fontSmall;
            lblSubtitle.AutoSize = true;
            lblSubtitle.Location = UiMetrics.At(29, 42);
            lblSubtitle.Tag = "muted";
            lblSubtitle.BackColor = Color.FromArgb(243, 245, 249);

            chkTopMost = new SoftCheckBox();
            chkTopMost.Text = "窗口置顶";
            chkTopMost.AccessibleName = "窗口置顶";
            chkTopMost.Size = UiMetrics.SizeOf(108, 22);
            chkTopMost.Checked = alwaysOnTop;
            chkTopMost.CheckedChanged += delegate
            {
                alwaysOnTop = chkTopMost.Checked;
                TopMost = alwaysOnTop;
                if (initialized)
                {
                    SaveSettings();
                }
            };

            btnSettings = new SoftIconButton();
            btnSettings.Kind = SoftIconKind.Settings;
            btnSettings.Size = UiMetrics.SizeOf(36, 36);
            btnSettings.AccessibleName = "设置";
            btnSettings.Click += delegate
            {
                ShowPage(1);
            };
            tips.SetToolTip(btnSettings, "设置");

            hp.Controls.Add(lblTitle);
            hp.Controls.Add(lblSubtitle);
            hp.Controls.Add(chkTopMost);
            hp.Controls.Add(btnSettings);
            hp.Resize += delegate
            {
                btnSettings.Location = new Point(
                    Math.Max(S(120), hp.ClientSize.Width - S(26) - btnSettings.Width),
                    S(17));
                chkTopMost.Location = new Point(
                    Math.Max(S(20), btnSettings.Left - S(14) - chkTopMost.Width),
                    S(24));
            };
            return hp;
        }

        private Panel BuildStatusBar()
        {
            Panel sb = new Panel();
            sb.Dock = DockStyle.Fill;
            sb.Margin = new Padding(0);
            sb.Tag = "bg";
            sb.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (palette == null)
                {
                    return;
                }
                using (Pen pen = new Pen(palette.Hairline))
                {
                    e.Graphics.DrawLine(pen, S(24), 0, sb.Width - S(24), 0);
                }
            };

            lblDeveloper = new Label();
            lblDeveloper.Text = AppTitle + " · 开发者 NIL · 引擎 nilaoda (MIT)";
            lblDeveloper.Font = fontSmall;
            lblDeveloper.AutoSize = true;
            lblDeveloper.Location = UiMetrics.At(28, 7);
            lblDeveloper.Tag = "muted";

            lblVersion = new Label();
            lblVersion.Text = "version " + AppVersion;
            lblVersion.Font = fontSmall;
            lblVersion.AutoSize = true;
            lblVersion.Tag = "muted";

            sb.Controls.Add(lblDeveloper);
            sb.Controls.Add(lblVersion);
            sb.Resize += delegate
            {
                lblVersion.Location = new Point(
                    Math.Max(S(28), sb.ClientSize.Width - lblVersion.Width - S(28)), S(7));
            };
            return sb;
        }

        // ————————————————————————————— 下载页 —————————————————————————————

        private Panel BuildDownloadPage()
        {
            Panel page = new Panel();
            page.BackColor = Color.FromArgb(243, 245, 249);
            page.Padding = Pad(24, 6, 24, 14);

            gridPage = new TableLayoutPanel();
            gridPage.Dock = DockStyle.Fill;
            gridPage.ColumnCount = 1;
            gridPage.RowCount = 3;
            gridPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            gridPage.RowStyles.Add(new RowStyle(SizeType.Absolute, S(250)));
            gridPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            gridPage.RowStyles.Add(new RowStyle(SizeType.Absolute, S(254)));
            gridPage.Margin = new Padding(0);
            gridPage.Padding = new Padding(0);

            cardParams = BuildParamsCard();
            cardTasks = BuildTasksCard();
            cardProgress = BuildProgressCard();
            cardParams.Margin = UiMetrics.P(0, 0, 0, 14);
            cardTasks.Margin = UiMetrics.P(0, 0, 0, 14);
            cardProgress.Margin = new Padding(0);

            gridPage.Controls.Add(cardParams, 0, 0);
            gridPage.Controls.Add(cardTasks, 0, 1);
            gridPage.Controls.Add(cardProgress, 0, 2);
            page.Controls.Add(gridPage);
            return page;
        }

        private SoftCard BuildParamsCard()
        {
            SoftCard card = new SoftCard();
            card.Dock = DockStyle.Fill;

            lblUrlCaption = MakeCaption("下载链接", 20, 24);
            txtUrl = MakeInput();
            txtUrl.AccessibleName = "下载链接";
            fieldUrl = new SoftField(txtUrl);
            fieldUrl.AccessibleName = "下载链接输入框";

            lblNameCaption = MakeCaption("保存名称", 20, 70);
            txtName = MakeInput();
            txtName.AccessibleName = "保存名称";
            fieldName = new SoftField(txtName);

            lblThreadCaption = MakeCaption("线程", 282, 70);
            numThreads = MakeNumber();
            numThreads.AccessibleName = "线程";
            numThreads.Minimum = 1m;
            numThreads.Maximum = 256m;
            numThreads.Value = threadsValue >= 1 && threadsValue <= 256
                ? threadsValue
                : 16;
            numThreads.TextChanged += delegate
            {
                if (!initialized)
                {
                    return;
                }
                int value;
                if (int.TryParse(numThreads.Text.Trim(), out value))
                {
                    threadsValue = value;
                    SaveSettings();
                }
            };
            fieldThread = new SoftField(numThreads);

            lblSpeedCaption = MakeCaption("限速KB/s", 402, 70);
            numMaxSpeed = MakeNumber();
            numMaxSpeed.AccessibleName = "限速KB/s";
            numMaxSpeed.Minimum = 0m;
            numMaxSpeed.Maximum = 100000000m;
            numMaxSpeed.Value = speedValue >= 0m && speedValue <= 100000000m
                ? speedValue
                : 0m;
            numMaxSpeed.TextChanged += delegate
            {
                if (!initialized)
                {
                    return;
                }
                decimal value;
                if (decimal.TryParse(numMaxSpeed.Text.Trim(), out value))
                {
                    speedValue = value;
                    SaveSettings();
                }
            };
            fieldSpeed = new SoftField(numMaxSpeed);

            lblProxyCaption = MakeCaption("代理", 590, 70);
            txtProxy = MakeInput();
            txtProxy.AccessibleName = "代理";
            fieldProxy = new SoftField(txtProxy);

            lblDirCaption = MakeCaption("保存目录", 20, 116);
            txtDir = MakeInput();
            txtDir.AccessibleName = "保存目录";
            fieldDir = new SoftField(txtDir);

            btnBrowse = new SoftButton();
            btnBrowse.Text = "浏览";
            btnBrowse.Style = SoftButtonStyle.Secondary;
            btnBrowse.AccessibleName = "浏览目录";
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

            btnOpen = new SoftButton();
            btnOpen.Text = "打开";
            btnOpen.Style = SoftButtonStyle.Secondary;
            btnOpen.AccessibleName = "打开目录";
            btnOpen.Click += delegate
            {
                OpenFolder(CurrentVideosDir());
            };

            btnDownload = new SoftButton();
            btnDownload.Text = "开始下载";
            btnDownload.Style = SoftButtonStyle.Primary;
            btnDownload.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            btnDownload.AccessibleName = "开始下载";
            btnDownload.Click += delegate
            {
                StartDownload();
            };

            btnStop = new SoftButton();
            btnStop.Text = "停止选中";
            btnStop.Style = SoftButtonStyle.Secondary;
            btnStop.AccessibleName = "停止选中";
            btnStop.Click += delegate
            {
                StopSelected();
            };

            btnRemove = new SoftButton();
            btnRemove.Text = "删除选中";
            btnRemove.Style = SoftButtonStyle.Danger;
            btnRemove.AccessibleName = "删除选中";
            btnRemove.Click += delegate
            {
                RemoveSelected();
            };

            btnClear = new SoftButton();
            btnClear.Text = "清空已完成";
            btnClear.Style = SoftButtonStyle.Ghost;
            btnClear.AccessibleName = "清空已完成";
            btnClear.Click += delegate
            {
                ClearFinished();
            };

            chkDelAfterDone = new SoftCheckBox();
            chkDelAfterDone.Text = "完成后删除临时分片文件";
            chkDelAfterDone.AccessibleName = "完成后删除临时分片文件";
            chkDelAfterDone.Size = UiMetrics.SizeOf(214, 22);
            chkDelAfterDone.Checked = deleteAfterDone;
            chkDelAfterDone.CheckedChanged += delegate
            {
                deleteAfterDone = chkDelAfterDone.Checked;
                if (initialized)
                {
                    SaveSettings();
                }
            };

            chkInfiniteRetry = new SoftCheckBox();
            chkInfiniteRetry.Text = "达到重连上限后继续下载";
            chkInfiniteRetry.AccessibleName = "达到重连上限后继续下载";
            chkInfiniteRetry.Size = UiMetrics.SizeOf(214, 22);
            chkInfiniteRetry.Checked = continueAfterRetryLimit;
            chkInfiniteRetry.CheckedChanged += delegate
            {
                continueAfterRetryLimit = chkInfiniteRetry.Checked;
                if (initialized)
                {
                    SaveSettings();
                }
            };

            chkResetRetryOnReconnect = new SoftCheckBox();
            chkResetRetryOnReconnect.Text = "重连成功后重置次数";
            chkResetRetryOnReconnect.AccessibleName = "重连成功后重置次数";
            chkResetRetryOnReconnect.Size = UiMetrics.SizeOf(190, 22);
            chkResetRetryOnReconnect.Checked = resetRetryOnReconnect;
            chkResetRetryOnReconnect.CheckedChanged += delegate
            {
                resetRetryOnReconnect = chkResetRetryOnReconnect.Checked;
                if (initialized)
                {
                    SaveSettings();
                }
            };

            card.Controls.Add(lblUrlCaption);
            card.Controls.Add(fieldUrl);
            card.Controls.Add(lblNameCaption);
            card.Controls.Add(fieldName);
            card.Controls.Add(lblThreadCaption);
            card.Controls.Add(fieldThread);
            card.Controls.Add(lblSpeedCaption);
            card.Controls.Add(fieldSpeed);
            card.Controls.Add(lblProxyCaption);
            card.Controls.Add(fieldProxy);
            card.Controls.Add(lblDirCaption);
            card.Controls.Add(fieldDir);
            card.Controls.Add(btnBrowse);
            card.Controls.Add(btnOpen);
            card.Controls.Add(btnDownload);
            card.Controls.Add(btnStop);
            card.Controls.Add(btnRemove);
            card.Controls.Add(btnClear);
            card.Controls.Add(chkDelAfterDone);
            card.Controls.Add(chkInfiniteRetry);
            card.Controls.Add(chkResetRetryOnReconnect);

            card.Resize += delegate
            {
                LayoutParams();
            };
            LayoutParams();
            return card;
        }

        private static int CaptionWidth(Label label)
        {
            if (label == null)
            {
                return 0;
            }
            return TextRenderer.MeasureText(label.Text, label.Font).Width;
        }

        /// <summary>按当前语言重新排布参数区：说明文字宽度变化后，输入框随之右移。</summary>
        private void LayoutParams()
        {
            if (cardParams == null || fieldUrl == null)
            {
                return;
            }
            int width = cardParams.ClientSize.Width;
            int left = S(20);
            int right = width - S(20);
            int gap = S(10);
            int rowGap = S(18);
            int height = S(38);
            int x;
            int labelTop;
            int w;

            // 第一行：下载链接
            labelTop = S(14) + (height - lblUrlCaption.Height) / 2;
            lblUrlCaption.Location = new Point(left, labelTop);
            x = left;
            w = CaptionWidth(lblUrlCaption) + gap;
            fieldUrl.SetBounds(x + w, S(14), Math.Max(S(140), right - x - w), height);

            // 第二行：保存名称 / 线程 / 限速 / 代理
            int y = S(60);
            x = left;
            lblNameCaption.Location = new Point(x, y + (height - lblNameCaption.Height) / 2);
            w = CaptionWidth(lblNameCaption) + gap;
            fieldName.SetBounds(x + w, y, S(170), height);
            x = fieldName.Right + rowGap;

            lblThreadCaption.Location = new Point(x, y + (height - lblThreadCaption.Height) / 2);
            w = CaptionWidth(lblThreadCaption) + gap;
            fieldThread.SetBounds(x + w, y, S(64), height);
            x = fieldThread.Right + rowGap;

            lblSpeedCaption.Location = new Point(x, y + (height - lblSpeedCaption.Height) / 2);
            w = CaptionWidth(lblSpeedCaption) + gap;
            fieldSpeed.SetBounds(x + w, y, S(78), height);
            x = fieldSpeed.Right + rowGap;

            lblProxyCaption.Location = new Point(x, y + (height - lblProxyCaption.Height) / 2);
            w = CaptionWidth(lblProxyCaption) + gap;
            fieldProxy.SetBounds(x + w, y, Math.Max(S(120), right - x - w), height);

            // 第三行：保存目录
            y = S(106);
            lblDirCaption.Location = new Point(left, y + (height - lblDirCaption.Height) / 2);
            w = CaptionWidth(lblDirCaption) + gap;
            fieldDir.SetBounds(left + w, y, Math.Max(S(140), right - left - w - S(180)), height);
            btnBrowse.SetBounds(right - S(174), S(108), S(80), S(34));
            btnOpen.SetBounds(right - S(88), S(108), S(80), S(34));

            // 按钮行
            btnDownload.SetBounds(left, S(156), S(134), S(40));
            btnStop.SetBounds(btnDownload.Right + S(8), S(156), S(104), S(40));
            btnRemove.SetBounds(btnStop.Right + S(8), S(156), S(104), S(40));
            btnClear.SetBounds(btnRemove.Right + S(8), S(156), S(112), S(40));

            // 勾选行：按各自文字宽度依次排布
            int checkTop = S(206);
            Control[] boxes = new Control[]
            {
                chkDelAfterDone, chkInfiniteRetry, chkResetRetryOnReconnect
            };
            x = left;
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].Location = new Point(x, checkTop);
                x = boxes[i].Right + S(22);
            }
        }

        /// <summary>图例按文字宽度重排（英文比中文长）。</summary>
        private void LayoutLegend()
        {
            if (legendDots.Count == 0 || cardProgress == null)
            {
                return;
            }
            int x = S(20);
            for (int i = 0; i < legendDots.Count; i++)
            {
                legendDots[i].Location = new Point(x, S(legendTops + 5));
                int labelLeft = x + S(14);
                legendLabels[i].Location = new Point(labelLeft, S(legendTops - 1));
                x = labelLeft + legendLabels[i].Width + S(16);
            }
        }

        private SoftCard BuildTasksCard()
        {
            SoftCard card = new SoftCard();
            card.Dock = DockStyle.Fill;

            lblTasksTitle = new Label();
            lblTasksTitle.Text = "任务列表";
            lblTasksTitle.Font = fontSection;
            lblTasksTitle.AutoSize = true;
            lblTasksTitle.Location = UiMetrics.At(20, 14);
            lblTasksTitle.Tag = "text";
            lblTasksTitle.BackColor = Color.FromArgb(243, 245, 249);

            lvTasks = new ListView();
            lvTasks.AccessibleName = "下载任务";
            lvTasks.View = View.Details;
            lvTasks.OwnerDraw = true;
            lvTasks.FullRowSelect = true;
            lvTasks.GridLines = false;
            lvTasks.HideSelection = false;
            lvTasks.MultiSelect = false;
            lvTasks.BorderStyle = BorderStyle.None;
            lvTasks.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lvTasks.Font = new Font("Microsoft YaHei UI", 9f);
            lvTasks.DrawColumnHeader += DrawTaskColumnHeader;
            lvTasks.DrawItem += DrawTaskItem;
            lvTasks.DrawSubItem += DrawTaskSubItem;
            lvTasks.Columns.Add("名称", S(300));
            lvTasks.Columns.Add("进度", S(88));
            lvTasks.Columns.Add("速度", S(124));
            lvTasks.Columns.Add("状态", S(106));
            lvTasks.Columns.Add("地址", S(240));
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

            card.Controls.Add(lblTasksTitle);
            card.Controls.Add(lvTasks);
            card.Resize += delegate
            {
                LayoutTasks();
            };
            LayoutTasks();
            return card;
        }

        private void LayoutTasks()
        {
            if (cardTasks == null || lvTasks == null)
            {
                return;
            }
            int width = cardTasks.ClientSize.Width;
            int height = cardTasks.ClientSize.Height;
            lvTasks.SetBounds(
                S(16), S(40),
                Math.Max(S(200), width - S(32)),
                Math.Max(S(60), height - S(56)));
            if (lvTasks.Columns.Count == 5)
            {
                int fixedWidth = S(300) + S(88) + S(124) + S(106);
                lvTasks.Columns[4].Width = Math.Max(
                    S(120), width - S(32) - fixedWidth);
            }
        }

        private SoftCard BuildProgressCard()
        {
            SoftCard card = new SoftCard();
            card.Dock = DockStyle.Fill;

            lblProgressTitle = new Label();
            lblProgressTitle.Text = "下载进度";
            lblProgressTitle.Font = fontSection;
            lblProgressTitle.AutoSize = true;
            lblProgressTitle.Location = UiMetrics.At(20, 14);
            lblProgressTitle.Tag = "text";
            lblProgressTitle.BackColor = Color.FromArgb(243, 245, 249);

            progressBar = new SoftBar();
            progressBar.AccessibleName = "下载进度条";

            lblSpeed = new Label();
            lblSpeed.Text = "速度:  --";
            lblSpeed.AutoSize = false;
            lblSpeed.AutoEllipsis = true;
            lblSpeed.TextAlign = ContentAlignment.MiddleLeft;
            lblSpeed.Tag = "success";
            lblSpeed.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);

            lblProgress = new Label();
            lblProgress.Text = "进度:  --";
            lblProgress.AutoSize = false;
            lblProgress.AutoEllipsis = true;
            lblProgress.TextAlign = ContentAlignment.MiddleLeft;
            lblProgress.Tag = "text";

            stateBox = new DotPanel();
            stateBox.BackColor = Color.Silver;

            lblState = new Label();
            lblState.Text = "状态:  就绪";
            lblState.AutoSize = false;
            lblState.AutoEllipsis = true;
            lblState.TextAlign = ContentAlignment.MiddleLeft;
            lblState.Tag = "text";

            card.Controls.Add(lblProgressTitle);
            card.Controls.Add(progressBar);
            card.Controls.Add(lblSpeed);
            card.Controls.Add(lblProgress);
            card.Controls.Add(stateBox);
            card.Controls.Add(lblState);

            AddLegend(card, 20, 88, "信息");
            AddLegend(card, 86, 88, "成功");
            AddLegend(card, 152, 88, "警告");
            AddLegend(card, 218, 88, "失败");

            txtLog = new RichTextBox();
            txtLog.AccessibleName = "运行日志";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtLog.WordWrap = false;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.DetectUrls = false;
            txtLog.Font = new Font("Consolas", 9f);
            txtLog.BackColor = Color.FromArgb(19, 21, 25);
            txtLog.ForeColor = Color.FromArgb(210, 215, 221);

            fieldLog = new SoftField(txtLog);
            fieldLog.StretchInner = true;
            fieldLog.InnerPadding = UiMetrics.P(8);
            fieldLog.AccessibleName = "日志区域";
            card.Controls.Add(fieldLog);

            card.Resize += delegate
            {
                LayoutProgress();
            };
            LayoutProgress();
            return card;
        }

        private void AddLegend(SoftCard card, int left, int top, string text)
        {
            DotPanel dot = new DotPanel();
            dot.Tag = "legend";
            dot.Size = UiMetrics.SizeOf(9, 9);
            dot.Location = UiMetrics.At(left, top + 5);
            dot.Tag = text;
            if (text == "成功")
            {
                dot.BackColor = Color.FromArgb(70, 200, 120);
            }
            else if (text == "警告")
            {
                dot.BackColor = Color.FromArgb(240, 175, 60);
            }
            else if (text == "失败")
            {
                dot.BackColor = Color.FromArgb(240, 100, 100);
            }
            else
            {
                dot.BackColor = Color.FromArgb(150, 158, 168);
            }

            Label label = new Label();
            label.Text = text;
            label.Font = fontSmall;
            label.AutoSize = true;
            label.Location = UiMetrics.At(left + 14, top - 1);
            label.Tag = "muted";
            label.BackColor = Color.FromArgb(243, 245, 249);

            card.Controls.Add(dot);
            card.Controls.Add(label);
            legendDots.Add(dot);
            legendLabels.Add(label);
            legendTops = top;
        }

        private void LayoutProgress()
        {
            if (cardProgress == null)
            {
                return;
            }
            int width = cardProgress.ClientSize.Width;
            int height = cardProgress.ClientSize.Height;
            int available = Math.Max(S(240), width - S(40));
            int speedWidth = Math.Max(S(210), (int)(available * 0.32f));
            int progressWidth = Math.Max(S(220), (int)(available * 0.36f));

            progressBar.SetBounds(S(20), S(42), available, S(8));
            lblSpeed.SetBounds(S(20), S(58), speedWidth, S(20));
            int progressLeft = S(20) + speedWidth + S(16);
            lblProgress.SetBounds(progressLeft, S(58), progressWidth, S(20));
            int stateLeft = progressLeft + progressWidth + S(14);
            stateBox.SetBounds(stateLeft, S(64), S(10), S(10));
            lblState.SetBounds(
                stateLeft + S(16),
                S(58),
                Math.Max(S(90), width - S(20) - stateLeft - S(16)),
                S(20));
            fieldLog.SetBounds(
                S(20),
                S(110),
                available,
                Math.Max(S(80), height - S(110) - S(18)));
        }

        // ————————————————————————————— 设置页 —————————————————————————————

        private Panel BuildSettingsPage()
        {
            Panel page = new Panel();
            page.BackColor = Color.FromArgb(243, 245, 249);

            gridSettings = new TableLayoutPanel();
            gridSettings.Dock = DockStyle.Fill;
            gridSettings.ColumnCount = 2;
            gridSettings.RowCount = 1;
            gridSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(196)));
            gridSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            gridSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            gridSettings.Margin = new Padding(0);
            gridSettings.Padding = new Padding(0);

            settingsNav = new Panel();
            settingsNav.Dock = DockStyle.Fill;
            settingsNav.Margin = new Padding(0);
            settingsNav.Tag = "bg";
            settingsNav.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (palette == null)
                {
                    return;
                }
                using (Pen pen = new Pen(palette.Hairline))
                {
                    e.Graphics.DrawLine(
                        pen, settingsNav.Width - 1, 0,
                        settingsNav.Width - 1, settingsNav.Height);
                }
            };

            Label navTitle = new Label();
            navTitle.Text = "设置";
            navTitle.Font = fontHeading;
            navTitle.AutoSize = true;
            navTitle.Location = UiMetrics.At(26, 22);
            navTitle.Tag = "heading";
            navTitle.BackColor = Color.FromArgb(243, 245, 249);

            sideTheme = MakeSideItem("主题", 76);
            sideTheme.Click += delegate
            {
                ShowSettingsPane(0);
            };

            sideFfmpeg = MakeSideItem("FFmpeg", 122);
            sideFfmpeg.Click += delegate
            {
                ShowSettingsPane(1);
            };

            sideLang = MakeSideItem("语言", 168);
            sideLang.Click += delegate
            {
                ShowSettingsPane(2);
            };

            sideAbout = MakeSideItem("关于", 214);
            sideAbout.Click += delegate
            {
                ShowSettingsPane(3);
            };
            sideAbout.Active = false;

            settingsNav.Controls.Add(navTitle);
            settingsNav.Controls.Add(sideTheme);
            settingsNav.Controls.Add(sideFfmpeg);
            settingsNav.Controls.Add(sideLang);
            settingsNav.Controls.Add(sideAbout);

            settingsBody = new Panel();
            settingsBody.Dock = DockStyle.Fill;
            settingsBody.Margin = new Padding(0);
            settingsBody.Tag = "bg";
            settingsBody.Padding = Pad(26, 8, 26, 12);

            themePane = BuildThemePane();
            ffmpegPane = BuildFfmpegPane();
            langPane = BuildLangPane();
            aboutPane = BuildAboutPane();
            themePane.Tag = "bg";
            ffmpegPane.Tag = "bg";
            langPane.Tag = "bg";
            aboutPane.Tag = "bg";
            themePane.Dock = DockStyle.Fill;
            ffmpegPane.Dock = DockStyle.Fill;
            langPane.Dock = DockStyle.Fill;
            aboutPane.Dock = DockStyle.Fill;
            themePane.Visible = true;
            ffmpegPane.Visible = false;
            langPane.Visible = false;
            aboutPane.Visible = false;
            settingsBody.Controls.Add(themePane);
            settingsBody.Controls.Add(ffmpegPane);
            settingsBody.Controls.Add(langPane);
            settingsBody.Controls.Add(aboutPane);

            gridSettings.Controls.Add(settingsNav, 0, 0);
            gridSettings.Controls.Add(settingsBody, 1, 0);
            page.Controls.Add(gridSettings);
            ShowSettingsPane(0);
            return page;
        }

        private SideItem MakeSideItem(string text, int top)
        {
            SideItem item = new SideItem();
            item.Text = text;
            item.SetBounds(S(16), S(top), S(164), S(40));
            return item;
        }

        private void ShowSettingsPane(int index)
        {
            if (themePane == null)
            {
                return;
            }
            Panel[] panes = new Panel[] { themePane, ffmpegPane, langPane, aboutPane };
            SideItem[] items = new SideItem[] { sideTheme, sideFfmpeg, sideLang, sideAbout };
            for (int i = 0; i < panes.Length; i++)
            {
                bool active = i == index;
                panes[i].Visible = active;
                items[i].Active = active;
                items[i].Invalidate();
            }
        }

        // ———————————————— 主题 ————————————————

        private Panel BuildThemePane()
        {
            Panel pane = new Panel();
            pane.BackColor = Color.FromArgb(243, 245, 249);

            lblThemeHeading = new Label();
            lblThemeHeading.Text = "主题";
            lblThemeHeading.Font = fontHeading;
            lblThemeHeading.AutoSize = true;
            lblThemeHeading.Location = UiMetrics.At(0, 4);
            lblThemeHeading.Tag = "heading";
            lblThemeHeading.BackColor = Color.FromArgb(243, 245, 249);

            lblThemeDesc = new Label();
            lblThemeDesc.Text = "选择强调色与外观模式，修改立即生效并自动保存。";
            lblThemeDesc.Font = fontSmall;
            lblThemeDesc.AutoSize = true;
            lblThemeDesc.Location = UiMetrics.At(2, 40);
            lblThemeDesc.Tag = "muted";
            lblThemeDesc.BackColor = Color.FromArgb(243, 245, 249);

            cardAccent = new SoftCard();
            cardMode = new SoftCard();

            Label accentTitle = new Label();
            accentTitle.Text = "主题色";
            accentTitle.Font = fontSection;
            accentTitle.AutoSize = true;
            accentTitle.Location = UiMetrics.At(18, 16);
            accentTitle.Tag = "text";
            accentTitle.BackColor = Color.FromArgb(243, 245, 249);

            swFish = new AccentSwatch();
            swFish.Kind = AccentKind.FishBlue;
            swFish.Title = "大肥鱼蓝";
            swFish.Hint = "明亮海蓝，默认配色";
            swFish.AccessibleName = "大肥鱼蓝主题";
            swFish.Picked += delegate
            {
                SelectAccent(AccentKind.FishBlue);
            };

            swClaude = new AccentSwatch();
            swClaude.Kind = AccentKind.ClaudeOrange;
            swClaude.Title = "Claude 橙";
            swClaude.Hint = "暖橙，柔和护眼";
            swClaude.AccessibleName = "Claude橙主题";
            swClaude.Picked += delegate
            {
                SelectAccent(AccentKind.ClaudeOrange);
            };

            cardAccent.Controls.Add(accentTitle);
            cardAccent.Controls.Add(swFish);
            cardAccent.Controls.Add(swClaude);

            Label modeTitle = new Label();
            modeTitle.Text = "外观模式";
            modeTitle.Font = fontSection;
            modeTitle.AutoSize = true;
            modeTitle.Location = UiMetrics.At(18, 16);
            modeTitle.Tag = "text";
            modeTitle.BackColor = Color.FromArgb(243, 245, 249);

            segMode = new SegmentedSwitch();
            segMode.AccessibleName = "外观模式";
            segMode.SelectedIndexChanged += delegate
            {
                if (!initialized)
                {
                    return;
                }
                themeMode = IndexToMode(segMode.SelectedIndex);
                SaveSettings();
                ApplyTheme();
            };

            lblModeHint = new Label();
            lblModeHint.Text = "浅色适合白天，深色降低夜间亮度；跟随系统会随 Windows 的设置自动切换。";
            lblModeHint.Font = fontSmall;
            lblModeHint.AutoSize = false;
            lblModeHint.Tag = "muted";
            lblModeHint.BackColor = Color.FromArgb(243, 245, 249);

            cardMode.Controls.Add(modeTitle);
            cardMode.Controls.Add(segMode);
            cardMode.Controls.Add(lblModeHint);

            pane.Controls.Add(lblThemeHeading);
            pane.Controls.Add(lblThemeDesc);
            pane.Controls.Add(cardAccent);
            pane.Controls.Add(cardMode);
            pane.Resize += delegate
            {
                LayoutThemePane();
            };
            LayoutThemePane();
            return pane;
        }

        private void LayoutThemePane()
        {
            if (themePane == null || cardAccent == null)
            {
                return;
            }
            int width = themePane.ClientSize.Width;
            int gap = S(16);
            int cardWidth = Math.Max(S(260), (width - gap) / 2);
            int top = S(76);
            int cardHeight = S(214);

            cardAccent.SetBounds(0, top, cardWidth, cardHeight);
            cardMode.SetBounds(
                cardWidth + gap, top,
                Math.Max(S(260), width - gap - cardWidth), cardHeight);

            swFish.SetBounds(S(18), S(50), Math.Max(S(120), cardWidth - S(36)), S(66));
            swClaude.SetBounds(S(18), S(122), Math.Max(S(120), cardWidth - S(36)), S(66));

            int modeWidth = cardMode.ClientSize.Width;
            segMode.SetBounds(S(18), S(52), Math.Max(S(160), modeWidth - S(36)), S(44));
            lblModeHint.SetBounds(
                S(18), S(110), Math.Max(S(120), modeWidth - S(36)), S(86));
        }

        // ———————————————— FFmpeg ————————————————

        private Panel BuildFfmpegPane()
        {
            Panel pane = new Panel();
            pane.BackColor = Color.FromArgb(243, 245, 249);

            Label heading = new Label();
            heading.Text = "FFmpeg";
            heading.Font = fontHeading;
            heading.AutoSize = true;
            heading.Location = UiMetrics.At(0, 4);
            heading.Tag = "heading";
            heading.BackColor = Color.FromArgb(243, 245, 249);

            Label desc = new Label();
            desc.Text = "选择 FFmpeg 来源，下载时优先使用所选项。";
            desc.Font = fontSmall;
            desc.AutoSize = true;
            desc.Location = UiMetrics.At(2, 40);
            desc.Tag = "muted";
            desc.BackColor = Color.FromArgb(243, 245, 249);

            cardFfmpeg = new SoftCard();
            cardFfmpeg.SetBounds(0, S(76), S(620), S(232));

            Label sourceTitle = new Label();
            sourceTitle.Text = "FFmpeg 来源";
            sourceTitle.Font = fontSection;
            sourceTitle.AutoSize = true;
            sourceTitle.Location = UiMetrics.At(20, 16);
            sourceTitle.Tag = "text";
            sourceTitle.BackColor = Color.FromArgb(243, 245, 249);

            ffmpegSwitch = new SegmentedSwitch();
            ffmpegSwitch.AccessibleName = "FFmpeg 来源";
            ffmpegSwitch.SetLabels(new string[] { "使用软件目录", "自定义目录" }, false);
            ffmpegSwitch.SetSelected(0, false);
            ffmpegSwitch.SelectedIndexChanged += delegate
            {
                if (!initialized)
                {
                    return;
                }
                ffmpegMode = ffmpegSwitch.SelectedIndex == 1 ? 1 : 0;
                ApplyFfmpegSelection();
                UpdateFfmpegStatus();
                SaveSettings();
            };

            Label pathTitle = new Label();
            pathTitle.Text = "ffmpeg.exe 所在文件夹";
            pathTitle.Font = fontSmall;
            pathTitle.AutoSize = true;
            pathTitle.Location = UiMetrics.At(20, 104);
            pathTitle.Tag = "muted";
            pathTitle.BackColor = Color.FromArgb(243, 245, 249);

            txtFfmpegDir = new TextBox();
            txtFfmpegDir.ReadOnly = true;
            txtFfmpegDir.BorderStyle = BorderStyle.None;
            txtFfmpegDir.Font = new Font("Microsoft YaHei UI", 9f);
            txtFfmpegDir.AccessibleName = "ffmpeg 文件夹";
            fieldFfmpegDir = new SoftField(txtFfmpegDir);
            fieldFfmpegDir.SetBounds(S(20), S(124), S(430), S(36));

            btnFfmpegPick = new SoftButton();
            btnFfmpegPick.Text = "选择文件夹";
            btnFfmpegPick.Style = SoftButtonStyle.Secondary;
            btnFfmpegPick.AccessibleName = "选择文件夹";
            btnFfmpegPick.Click += delegate
            {
                ChooseFfmpegFolder();
            };

            btnFfmpegAuto = new SoftButton();
            btnFfmpegAuto.Text = "自动检测";
            btnFfmpegAuto.Style = SoftButtonStyle.Secondary;
            btnFfmpegAuto.AccessibleName = "自动检测";
            btnFfmpegAuto.Click += delegate
            {
                AutoDetectFfmpeg();
            };

            lblFfmpegStatus = new Label();
            lblFfmpegStatus.Text = "";
            lblFfmpegStatus.Font = fontSmall;
            lblFfmpegStatus.AutoSize = false;
            lblFfmpegStatus.AutoEllipsis = true;
            lblFfmpegStatus.Tag = "keep-color";
            lblFfmpegStatus.BackColor = Color.FromArgb(243, 245, 249);

            cardFfmpeg.Controls.Add(sourceTitle);
            cardFfmpeg.Controls.Add(ffmpegSwitch);
            cardFfmpeg.Controls.Add(pathTitle);
            cardFfmpeg.Controls.Add(fieldFfmpegDir);
            cardFfmpeg.Controls.Add(btnFfmpegPick);
            cardFfmpeg.Controls.Add(btnFfmpegAuto);
            cardFfmpeg.Controls.Add(lblFfmpegStatus);

            pane.Controls.Add(heading);
            pane.Controls.Add(desc);
            pane.Controls.Add(cardFfmpeg);
            pane.Resize += delegate
            {
                LayoutFfmpegPane();
            };
            LayoutFfmpegPane();
            return pane;
        }

        private void LayoutFfmpegPane()
        {
            if (ffmpegPane == null || cardFfmpeg == null)
            {
                return;
            }
            int width = Math.Min(S(720), Math.Max(S(420), ffmpegPane.ClientSize.Width));
            cardFfmpeg.SetBounds(0, S(76), width, S(232));

            int inner = width - S(40);
            ffmpegSwitch.SetBounds(S(20), S(46), Math.Max(S(200), inner), S(42));
            fieldFfmpegDir.SetBounds(
                S(20), S(124), Math.Max(S(160), inner - S(196)), S(36));
            btnFfmpegPick.SetBounds(
                width - S(200), S(124), S(88), S(36));
            btnFfmpegAuto.SetBounds(
                width - S(104), S(124), S(84), S(36));
            lblFfmpegStatus.SetBounds(S(20), S(172), Math.Max(S(120), inner), S(40));
        }

        /// <summary>选择包含 ffmpeg.exe 的文件夹。</summary>
        private void ChooseFfmpegFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = Lang.Translate("请选择 ffmpeg.exe 所在的文件夹");
                if (!string.IsNullOrEmpty(ffmpegDir) && Directory.Exists(ffmpegDir))
                {
                    dialog.SelectedPath = ffmpegDir;
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string dir = dialog.SelectedPath;
                if (!File.Exists(Path.Combine(dir, "ffmpeg.exe")))
                {
                    MessageBox.Show(
                        this,
                        Lang.Translate("该文件夹里没有 ffmpeg.exe，请重新选择。"),
                        Lang.Translate("提示"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
                ffmpegDir = dir;
                ffmpegMode = 1;
                txtFfmpegDir.Text = dir;
                ffmpegSwitch.SetSelected(1, true);
                ApplyFfmpegSelection();
                UpdateFfmpegStatus();
                SaveSettings();
            }
        }

        /// <summary>在系统 PATH 里找 ffmpeg.exe。</summary>
        private void AutoDetectFfmpeg()
        {
            string found = FindFfmpegOnPath();
            if (string.IsNullOrEmpty(found))
            {
                MessageBox.Show(
                    this,
                    Lang.Translate("系统 PATH 中未找到 ffmpeg"),
                    Lang.Translate("提示"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            ffmpegDir = found;
            ffmpegMode = 1;
            txtFfmpegDir.Text = found;
            ffmpegSwitch.SetSelected(1, true);
            ApplyFfmpegSelection();
            UpdateFfmpegStatus();
            SaveSettings();
        }

        private static string FindFfmpegOnPath()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                string[] parts = path.Split(Path.PathSeparator);
                for (int i = 0; i < parts.Length; i++)
                {
                    string dir = parts[i].Trim().Trim('"');
                    if (dir.Length == 0)
                    {
                        continue;
                    }
                    try
                    {
                        if (File.Exists(Path.Combine(dir, "ffmpeg.exe")))
                        {
                            return dir;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return "";
        }

        private void UpdateFfmpegStatus()
        {
            if (lblFfmpegStatus == null)
            {
                return;
            }
            bool custom = ffmpegMode == 1 && !string.IsNullOrEmpty(ffmpegDir);
            if (txtFfmpegDir != null)
            {
                txtFfmpegDir.Text = custom ? ffmpegDir : Program.AppDir();
            }
            string file = custom
                ? Path.Combine(ffmpegDir, "ffmpeg.exe")
                : Path.Combine(Program.AppDir(), "ffmpeg.exe");
            bool exists = File.Exists(file);
            lblFfmpegStatus.ForeColor = exists
                ? (palette != null ? palette.Success : Color.Green)
                : (palette != null ? palette.Danger : Color.Red);
            lblFfmpegStatus.Text = file;
        }

        // ———————————————— 语言 ————————————————

        private Panel BuildLangPane()
        {
            Panel pane = new Panel();
            pane.BackColor = Color.FromArgb(243, 245, 249);

            Label heading = new Label();
            heading.Text = "语言";
            heading.Font = fontHeading;
            heading.AutoSize = true;
            heading.Location = UiMetrics.At(0, 4);
            heading.Tag = "heading";
            heading.BackColor = Color.FromArgb(243, 245, 249);

            Label desc = new Label();
            desc.Text = "语言与界面文字，切换后立即生效。";
            desc.Font = fontSmall;
            desc.AutoSize = true;
            desc.Location = UiMetrics.At(2, 40);
            desc.Tag = "muted";
            desc.BackColor = Color.FromArgb(243, 245, 249);

            cardLang = new SoftCard();
            cardLang.SetBounds(0, S(76), S(420), S(226));

            string[] texts = new string[] { "简体中文", "繁體中文", "English", "跟随系统" };
            LangMode[] modes = new LangMode[]
            {
                LangMode.ZhHans, LangMode.ZhHant, LangMode.En, LangMode.System
            };
            langRows = new SideItem[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                SideItem row = new SideItem();
                row.Text = texts[i];
                row.SetBounds(S(16), S(16 + i * 50), S(388), S(42));
                row.Active = false;
                LangMode mode = modes[i];
                row.Click += delegate
                {
                    SwitchLanguage(mode);
                };
                langRows[i] = row;
                cardLang.Controls.Add(row);
            }

            pane.Controls.Add(heading);
            pane.Controls.Add(desc);
            pane.Controls.Add(cardLang);
            pane.Resize += delegate
            {
                LayoutLangPane();
            };
            LayoutLangPane();
            return pane;
        }

        private void LayoutLangPane()
        {
            if (langPane == null || cardLang == null)
            {
                return;
            }
            int width = Math.Min(S(460), Math.Max(S(320), langPane.ClientSize.Width));
            cardLang.SetBounds(0, S(76), width, S(226));
            if (langRows != null)
            {
                for (int i = 0; i < langRows.Length; i++)
                {
                    langRows[i].SetBounds(
                        S(16), S(16 + i * 50), Math.Max(S(200), width - S(32)), S(42));
                }
            }
        }

        private void SwitchLanguage(LangMode mode)
        {
            if (mode == Lang.Setting)
            {
                UpdateLangRows();
                return;
            }
            if (HasRunningJobs())
            {
                MessageBox.Show(
                    this,
                    Lang.Translate("请先停止正在进行的下载任务。"),
                    Lang.Translate("提示"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                UpdateLangRows();
                return;
            }
            DialogResult answer = MessageBox.Show(
                this,
                Lang.Translate("切换语言需要重新打开软件才能生效，是否立即重启？"),
                Lang.Translate("提示"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                // 用户选择不重启：回退到切换前的选择，不写入设置
                RevertLanguageSelection();
                return;
            }

            CommitLanguageChange(mode);
            RestartApplication();
        }

        /// <summary>把语言选择落盘（真正切换在重启后生效）。</summary>
        private void CommitLanguageChange(LangMode mode)
        {
            Lang.Setting = mode;
            langMode = mode;
            SaveSettings();
        }

        /// <summary>放弃切换：只是把左侧选中态刷回当前设置。</summary>
        private void RevertLanguageSelection()
        {
            UpdateLangRows();
        }

        private bool HasRunningJobs()
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                DownloadJob job = jobs[i];
                if (job.RetryTimer != null ||
                    (job.Proc != null && !HasExited(job.Proc)))
                {
                    return true;
                }
            }
            return false;
        }

        private void RestartApplication()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(Application.ExecutablePath);
                info.UseShellExecute = true;
                info.WorkingDirectory = Program.AppDir();
                Process.Start(info);
            }
            catch
            {
            }
            Application.Exit();
        }

        // ———————————————— 关于 ————————————————

        private Panel BuildAboutPane()
        {
            Panel pane = new Panel();
            pane.BackColor = Color.FromArgb(243, 245, 249);

            Label heading = new Label();
            heading.Text = "关于";
            heading.Font = fontHeading;
            heading.AutoSize = true;
            heading.Location = UiMetrics.At(0, 4);
            heading.Tag = "heading";
            heading.BackColor = Color.FromArgb(243, 245, 249);

            cardAbout = new SoftCard();
            cardAbout.SetBounds(0, S(76), S(560), S(268));

            Label name = new Label();
            name.Text = AppTitle;
            name.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            name.AutoSize = true;
            name.Location = UiMetrics.At(22, 20);
            name.Tag = "text";
            name.BackColor = Color.FromArgb(243, 245, 249);

            lblAboutVersion = new Label();
            lblAboutVersion.Text = "版本 " + AppVersion + " · 图形界面外壳";
            lblAboutVersion.Font = fontBody;
            lblAboutVersion.AutoSize = true;
            lblAboutVersion.Location = UiMetrics.At(22, 52);
            lblAboutVersion.Tag = "muted";
            lblAboutVersion.BackColor = Color.FromArgb(243, 245, 249);

            Label engineTitle = new Label();
            engineTitle.Text = "下载引擎";
            engineTitle.Font = fontSection;
            engineTitle.AutoSize = true;
            engineTitle.Location = UiMetrics.At(22, 86);
            engineTitle.Tag = "text";
            engineTitle.BackColor = Color.FromArgb(243, 245, 249);

            lblAboutEngine = new Label();
            lblAboutEngine.Text = "N_m3u8DL-CLI-engine.exe（原作者 nilaoda，MIT 许可证）";
            lblAboutEngine.Font = fontSmall;
            lblAboutEngine.AutoSize = true;
            lblAboutEngine.Location = UiMetrics.At(22, 112);
            lblAboutEngine.Tag = "muted";
            lblAboutEngine.BackColor = Color.FromArgb(243, 245, 249);

            lnkRepo = new LinkLabel();
            lnkRepo.Text = RepoUrl;
            lnkRepo.Font = fontSmall;
            lnkRepo.AutoSize = true;
            lnkRepo.Location = UiMetrics.At(22, 140);
            lnkRepo.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkRepo.Click += delegate
            {
                OpenRepo();
            };

            btnCheckUpdate = new SoftButton();
            btnCheckUpdate.Text = "检查更新";
            btnCheckUpdate.Style = SoftButtonStyle.Secondary;
            btnCheckUpdate.AccessibleName = "检查更新";
            btnCheckUpdate.SetBounds(S(22), S(178), S(112), S(36));
            btnCheckUpdate.Click += delegate
            {
                StartUpdateCheck();
            };

            btnOpenRelease = new SoftButton();
            btnOpenRelease.Text = "打开下载页";
            btnOpenRelease.Style = SoftButtonStyle.Primary;
            btnOpenRelease.AccessibleName = "打开下载页";
            btnOpenRelease.SetBounds(S(146), S(178), S(120), S(36));
            btnOpenRelease.Visible = false;
            btnOpenRelease.Click += delegate
            {
                OpenReleasePage();
            };

            lblUpdateStatus = new Label();
            lblUpdateStatus.Text = "";
            lblUpdateStatus.Font = fontSmall;
            lblUpdateStatus.AutoSize = false;
            lblUpdateStatus.AutoEllipsis = true;
            lblUpdateStatus.Tag = "muted";
            lblUpdateStatus.SetBounds(S(22), S(222), S(510), S(24));
            lblUpdateStatus.BackColor = Color.FromArgb(243, 245, 249);

            cardAbout.Controls.Add(name);
            cardAbout.Controls.Add(lblAboutVersion);
            cardAbout.Controls.Add(engineTitle);
            cardAbout.Controls.Add(lblAboutEngine);
            cardAbout.Controls.Add(lnkRepo);
            cardAbout.Controls.Add(btnCheckUpdate);
            cardAbout.Controls.Add(btnOpenRelease);
            cardAbout.Controls.Add(lblUpdateStatus);

            Label disclaimer = new Label();
            disclaimer.Text = "本程序仅用于个人学习与技术研究，请勿用于下载或传播无权使用的内容。";
            disclaimer.Font = fontSmall;
            disclaimer.AutoSize = false;
            disclaimer.SetBounds(S(0), S(360), S(560), S(44));
            disclaimer.Tag = "muted";
            disclaimer.BackColor = Color.FromArgb(243, 245, 249);

            pane.Controls.Add(heading);
            pane.Controls.Add(cardAbout);
            pane.Controls.Add(disclaimer);
            return pane;
        }

        private void StartUpdateCheck()
        {
            if (btnCheckUpdate == null || !btnCheckUpdate.Enabled)
            {
                return;
            }
            btnCheckUpdate.Enabled = false;
            btnCheckUpdate.Text = Lang.Translate("正在检查…");
            btnCheckUpdate.Invalidate();
            lblUpdateStatus.Text = "";
            btnOpenRelease.Visible = false;

            string current = AppVersion;
            string repo = RepoUrl;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                UpdateInfo info = UpdateChecker.Fetch(repo, current);
                BeginInvokeSafe(delegate
                {
                    OnUpdateChecked(info);
                });
            });
        }

        private void OnUpdateChecked(UpdateInfo info)
        {
            if (btnCheckUpdate == null || btnCheckUpdate.IsDisposed)
            {
                return;
            }
            btnCheckUpdate.Enabled = true;
            btnCheckUpdate.Text = Lang.Translate("检查更新");
            btnCheckUpdate.Invalidate();

            if (info == null || !info.Ok)
            {
                lblUpdateStatus.Text = Lang.Translate("检查失败，请稍后重试");
                return;
            }
            if (info.NoRelease)
            {
                lblUpdateStatus.Text = Lang.Translate("暂无发布版本");
                return;
            }
            if (!info.HasNewer)
            {
                lblUpdateStatus.Text =
                    Lang.Translate("已是最新版本") + " (" + AppVersion + ")";
                return;
            }
            lblUpdateStatus.Text =
                Lang.Translate("发现新版本") + " " + info.Tag +
                "  ·  " + Lang.Translate("当前版本") + " " + AppVersion;
            releaseUrl = info.Url;
            btnOpenRelease.Visible = true;
            btnOpenRelease.Text = Lang.Translate("打开下载页");
            btnOpenRelease.Invalidate();
        }

        private void OpenReleasePage()
        {
            string url = string.IsNullOrEmpty(releaseUrl) ? RepoUrl : releaseUrl;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private void OpenRepo()
        {
            try
            {
                Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        // ————————————————————————————— 页面切换 —————————————————————————————

        private void ShowPage(int index)
        {
            if (index < 0)
            {
                index = 0;
            }
            if (index > 1)
            {
                index = 1;
            }
            if (index == pageTarget && pageTimer != null && pageTimer.Enabled)
            {
                return;
            }
            pageStartT = pageT;
            pageStarted = DateTime.Now;
            pageTarget = index;
            navDownload.Active = index == 0;
            navSettings.Active = index == 1;
            navDownload.Invalidate();
            navSettings.Invalidate();
            btnSettings.Active = index == 1;
            btnSettings.Invalidate();
            if (pageTimer == null)
            {
                pageTimer = new System.Windows.Forms.Timer();
                pageTimer.Interval = 15;
                pageTimer.Tick += delegate
                {
                    StepPage();
                };
            }
            pageTimer.Start();
        }

        private void StepPage()
        {
            float elapsed = (float)(DateTime.Now - pageStarted).TotalMilliseconds;
            float k = elapsed / PageDurationMs;
            if (k >= 1f)
            {
                k = 1f;
                pageT = pageTarget;
                if (pageTimer != null)
                {
                    pageTimer.Stop();
                }
            }
            else
            {
                if (k < 0f)
                {
                    k = 0f;
                }
                pageT = pageStartT + (pageTarget - pageStartT) * k;
            }
            LayoutPages();
        }

        private void LayoutPages()
        {
            if (pageHost == null || downloadPage == null || settingsPage == null)
            {
                return;
            }
            int width = pageHost.ClientSize.Width;
            int height = pageHost.ClientSize.Height;
            int offset = (int)Math.Round(UiDraw.EaseOut(pageT) * width);
            downloadPage.SetBounds(-offset, 0, width, height);
            settingsPage.SetBounds(width - offset, 0, width, height);
        }

        // ————————————————————————————— 主题应用 —————————————————————————————

        private static int ModeToIndex(ThemeMode mode)
        {
            if (mode == ThemeMode.Light)
            {
                return 0;
            }
            if (mode == ThemeMode.Dark)
            {
                return 1;
            }
            return 2;
        }

        private static ThemeMode IndexToMode(int index)
        {
            if (index == 0)
            {
                return ThemeMode.Light;
            }
            if (index == 1)
            {
                return ThemeMode.Dark;
            }
            return ThemeMode.System;
        }

        private void SelectAccent(AccentKind kind)
        {
            accentKind = kind;
            swFish.SetSelected(kind == AccentKind.FishBlue, true);
            swClaude.SetSelected(kind == AccentKind.ClaudeOrange, true);
            if (initialized)
            {
                SaveSettings();
            }
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            currentDark = themeMode == ThemeMode.Dark ||
                (themeMode == ThemeMode.System && IsSystemDark());
            palette = UiPalette.Build(currentDark, accentKind);

            BackColor = palette.Window;
            if (gridRoot != null)
            {
                gridRoot.BackColor = palette.Window;
            }
            if (cardParams != null)
            {
                cardParams.ApplyColors(palette.Surface, palette.Border);
            }
            if (cardTasks != null)
            {
                cardTasks.ApplyColors(palette.Surface, palette.Border);
            }
            if (cardProgress != null)
            {
                cardProgress.ApplyColors(palette.Surface, palette.Border);
            }
            if (cardAccent != null)
            {
                cardAccent.ApplyColors(palette.Surface, palette.Border);
            }
            if (cardMode != null)
            {
                cardMode.ApplyColors(palette.Surface, palette.Border);
            }
            if (cardAbout != null)
            {
                cardAbout.ApplyColors(palette.Surface, palette.Border);
            }

            StyleTree(this, palette.Window);

            if (txtLog != null)
            {
                txtLog.BackColor = palette.LogBack;
                txtLog.ForeColor = palette.LogText;
            }
            if (fieldLog != null)
            {
                fieldLog.ApplyColors(palette.LogBack, palette.Border, palette.Accent);
            }
            if (lvTasks != null)
            {
                lvTasks.BackColor = palette.Surface;
                lvTasks.ForeColor = palette.Text;
                lvTasks.Invalidate();
            }

            if (cardProgress != null)
            {
                for (int i = 0; i < cardProgress.Controls.Count; i++)
                {
                    DotPanel dot = cardProgress.Controls[i] as DotPanel;
                    if (dot == null || dot == stateBox)
                    {
                        continue;
                    }
                    string kind = dot.Tag as string;
                    if (kind == "成功")
                    {
                        dot.BackColor = palette.Success;
                    }
                    else if (kind == "警告")
                    {
                        dot.BackColor = palette.Warning;
                    }
                    else if (kind == "失败")
                    {
                        dot.BackColor = palette.Danger;
                    }
                    else if (kind == "信息")
                    {
                        dot.BackColor = palette.Muted;
                    }
                }
            }

            if (segMode != null)
            {
                segMode.SetSelected(ModeToIndex(themeMode), false);
            }
            if (swFish != null)
            {
                swFish.SetSelected(accentKind == AccentKind.FishBlue, false);
                swClaude.SetSelected(accentKind == AccentKind.ClaudeOrange, false);
            }

            ApplyNativeTitleBar();
            RefreshSelection();
            UpdateFfmpegStatus();
        }

        private void StyleTree(Control parent, Color bg)
        {
            for (int i = 0; i < parent.Controls.Count; i++)
            {
                Control control = parent.Controls[i];
                Color childBg = bg;

                string tag = control.Tag as string;
                SoftCard card = control as SoftCard;
                if (card != null)
                {
                    card.ApplyColors(palette.Surface, palette.Border);
                    card.BackColor = bg;
                    childBg = palette.Surface;
                }
                else if (control is TableLayoutPanel)
                {
                    control.BackColor = palette.Window;
                    childBg = palette.Window;
                }
                else if (control is Panel && !(control is SoftField))
                {
                    if (tag == "rail")
                    {
                        control.BackColor = palette.Rail;
                        childBg = palette.Rail;
                    }
                    else if (tag == "bg")
                    {
                        control.BackColor = palette.Window;
                        childBg = palette.Window;
                    }
                    else
                    {
                        childBg = control.BackColor;
                    }
                }

                SoftField field = control as SoftField;
                if (field != null)
                {
                    field.ApplyColors(palette.Input, palette.Border, palette.Accent);
                    field.BackColor = bg;
                    childBg = palette.Input;
                }

                AnimControl anim = control as AnimControl;
                if (anim != null)
                {
                    anim.Surface = bg;
                    anim.ApplyPalette(palette);
                }

                SoftCheckBox check = control as SoftCheckBox;
                if (check != null)
                {
                    check.Surface = bg;
                    check.ApplyPalette(palette);
                }

                DotPanel dot = control as DotPanel;
                if (dot != null)
                {
                    dot.Surface = bg;
                    dot.Invalidate();
                }

                PictureBox picture = control as PictureBox;
                if (picture != null)
                {
                    picture.BackColor = bg;
                }

                Label label = control as Label;
                if (label != null)
                {
                    label.BackColor = bg;
                    if (tag != "keep-color")
                    {
                        label.ForeColor = RoleColor(palette, tag);
                    }
                }

                LinkLabel link = control as LinkLabel;
                if (link != null)
                {
                    link.BackColor = bg;
                    link.LinkColor = palette.Accent;
                    link.ActiveLinkColor = palette.AccentPress;
                    link.VisitedLinkColor = palette.Accent;
                }

                TextBox textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.BackColor = palette.Input;
                    textBox.ForeColor = palette.Text;
                }

                NumericUpDown number = control as NumericUpDown;
                if (number != null)
                {
                    number.BackColor = palette.Input;
                    number.ForeColor = palette.Text;
                }

                RichTextBox rich = control as RichTextBox;
                if (rich != null)
                {
                    rich.BackColor = palette.LogBack;
                    rich.ForeColor = palette.LogText;
                }

                StyleTree(control, childBg);
            }
        }

        private static Color RoleColor(UiPalette p, string role)
        {
            if (role == "text" || role == "heading")
            {
                return p.Text;
            }
            if (role == "success")
            {
                return p.Success;
            }
            if (role == "danger")
            {
                return p.Danger;
            }
            if (role == "accent")
            {
                return p.Accent;
            }
            return p.Muted;
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

        // ————————————————————————————— 控件工厂 —————————————————————————————

        private Label MakeCaption(string text, int left, int top)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9f);
            label.AutoSize = true;
            label.Location = UiMetrics.At(left, top);
            label.Tag = "muted";
            label.BackColor = Color.FromArgb(243, 245, 249);
            return label;
        }

        private TextBox MakeInput()
        {
            TextBox box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.Font = new Font("Microsoft YaHei UI", 9.5f);
            return box;
        }

        private SoftNumber MakeNumber()
        {
            SoftNumber number = new SoftNumber();
            number.TextAlign = HorizontalAlignment.Left;
            return number;
        }

        // ————————————————————————————— 任务列表自绘 —————————————————————————————

        private void DrawTaskColumnHeader(
            object sender,
            DrawListViewColumnHeaderEventArgs e)
        {
            if (palette == null)
            {
                e.DrawDefault = true;
                return;
            }
            using (SolidBrush brush = new SolidBrush(palette.SurfaceAlt))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                e.Font,
                new Rectangle(
                    e.Bounds.Left + S(10), e.Bounds.Top,
                    e.Bounds.Width - S(20), e.Bounds.Height),
                palette.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (Pen pen = new Pen(palette.Border))
            {
                e.Graphics.DrawLine(
                    pen,
                    e.Bounds.Left,
                    e.Bounds.Bottom - 1,
                    e.Bounds.Right,
                    e.Bounds.Bottom - 1);
            }
        }

        private void DrawTaskItem(object sender, DrawListViewItemEventArgs e)
        {
            if (palette == null)
            {
                e.DrawDefault = true;
                return;
            }
            Color background = e.Item.Selected
                ? palette.AccentSoft
                : palette.Surface;
            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            using (Pen pen = new Pen(palette.Hairline))
            {
                e.Graphics.DrawLine(
                    pen,
                    e.Bounds.Left,
                    e.Bounds.Bottom - 1,
                    e.Bounds.Right,
                    e.Bounds.Bottom - 1);
            }
        }

        private void DrawTaskSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (palette == null)
            {
                e.DrawDefault = true;
                return;
            }
            Color background = e.Item.Selected
                ? palette.AccentSoft
                : palette.Surface;
            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            Color foreground = palette.Text;
            if (e.ColumnIndex == 3)
            {
                foreground = StateColor(e.SubItem.Text);
            }
            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                e.SubItem.Font,
                new Rectangle(
                    e.Bounds.Left + S(10), e.Bounds.Top,
                    e.Bounds.Width - S(20), e.Bounds.Height),
                foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // ————————————————————————————— 本地化 —————————————————————————————

        private void CaptureOriginalTexts(Control parent)
        {
            for (int i = 0; i < parent.Controls.Count; i++)
            {
                Control control = parent.Controls[i];
                if (!string.IsNullOrEmpty(control.Text) && IsTextual(control))
                {
                    if (!originalTexts.ContainsKey(control))
                    {
                        originalTexts[control] = control.Text;
                    }
                }
                CaptureOriginalTexts(control);
            }
        }

        private static bool IsTextual(Control control)
        {
            return control is Label || control is Button || control is CheckBox ||
                control is SoftButton || control is SideItem || control is RailButton ||
                control is LinkLabel || control is SoftIconButton;
        }

        /// <summary>按当前语言刷新全部静态文案与动态标签。</summary>
        private void ApplyLanguage()
        {
            Lang.Current = Lang.Resolve(Lang.Setting);

            Control[] keys = new Control[originalTexts.Count];
            originalTexts.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                Control control = keys[i];
                if (control == null || control.IsDisposed)
                {
                    continue;
                }
                control.Text = Lang.Translate(originalTexts[control]);
            }

            if (segMode != null)
            {
                segMode.SetLabels(Translated(segModeLabels), true);
            }
            if (ffmpegSwitch != null)
            {
                ffmpegSwitch.SetLabels(Translated(ffmpegLabels), true);
            }
            if (swFish != null)
            {
                swFish.Title = Lang.Translate("大肥鱼蓝");
                swFish.Hint = Lang.Translate("明亮海蓝，默认配色");
                swFish.Invalidate();
            }
            if (swClaude != null)
            {
                swClaude.Title = Lang.Translate("Claude 橙");
                swClaude.Hint = Lang.Translate("暖橙，柔和护眼");
                swClaude.Invalidate();
            }
            if (lvTasks != null && lvTasks.Columns.Count == columnLabels.Length)
            {
                for (int i = 0; i < columnLabels.Length; i++)
                {
                    lvTasks.Columns[i].Text = Lang.Translate(columnLabels[i]);
                }
            }
            if (tips != null)
            {
                tips.SetToolTip(navDownload, Lang.Translate("下载"));
                tips.SetToolTip(navSettings, Lang.Translate("设置"));
                if (btnSettings != null)
                {
                    tips.SetToolTip(btnSettings, Lang.Translate("设置"));
                }
            }
            if (lblAboutVersion != null)
            {
                lblAboutVersion.Text = Lang.Translate("版本") + " " + AppVersion +
                    " · " + Lang.Translate("图形界面外壳");
            }
            if (lblDeveloper != null)
            {
                lblDeveloper.Text = AppTitle + " · " + Lang.Translate("开发者") +
                    " NIL · " + Lang.Translate("引擎") + " nilaoda (MIT)";
            }

            if (chkTopMost != null)
            {
                chkTopMost.AutoFit();
                chkTopMost.Left = Math.Max(
                    S(20), btnSettings.Left - S(14) - chkTopMost.Width);
            }
            if (chkDelAfterDone != null)
            {
                chkDelAfterDone.AutoFit();
                chkInfiniteRetry.AutoFit();
                chkResetRetryOnReconnect.AutoFit();
            }
            LayoutParams();
            LayoutLegend();
            UpdateLangRows();
            UpdateFfmpegStatus();
            RefreshSelection();
        }

        private string[] Translated(string[] source)
        {
            string[] result = new string[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = Lang.Translate(source[i]);
            }
            return result;
        }

        private void UpdateLangRows()
        {
            if (langRows == null)
            {
                return;
            }
            LangMode[] modes = new LangMode[]
            {
                LangMode.ZhHans, LangMode.ZhHant, LangMode.En, LangMode.System
            };
            for (int i = 0; i < langRows.Length; i++)
            {
                langRows[i].Active = modes[i] == Lang.Setting;
                langRows[i].Invalidate();
            }
        }
    }
}
