using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace M3u8DlGui
{
    internal interface IAnimated
    {
        bool StepAnimation();
    }

    /// <summary>
    /// 全局动画泵：只有存在进行中的动画时才运行计时器，空闲时自动停止。
    /// </summary>
    internal static class AnimBus
    {
        private static readonly List<IAnimated> Items = new List<IAnimated>();
        private static Timer timer;

        public static void Register(IAnimated item)
        {
            if (item == null || Items.Contains(item))
            {
                return;
            }
            Items.Add(item);
        }

        public static void Unregister(IAnimated item)
        {
            if (item != null)
            {
                Items.Remove(item);
            }
            Kick();
        }

        public static void Kick()
        {
            if (timer == null)
            {
                timer = new Timer();
                timer.Interval = 15;
                timer.Tick += OnTick;
            }
            if (!timer.Enabled)
            {
                timer.Start();
            }
        }

        private static void OnTick(object sender, EventArgs e)
        {
            bool busy = false;
            IAnimated[] snapshot = Items.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                IAnimated item = snapshot[i];
                bool moving = false;
                try
                {
                    moving = item.StepAnimation();
                }
                catch
                {
                    moving = false;
                }
                if (moving)
                {
                    busy = true;
                    Control control = item as Control;
                    if (control != null && control.IsHandleCreated)
                    {
                        control.Invalidate();
                    }
                }
            }
            if (!busy && timer != null)
            {
                timer.Stop();
            }
        }
    }

    internal abstract class AnimControl : Control, IAnimated
    {
        protected float Hover;
        protected float Press;
        protected bool Hovering;
        protected bool Pressing;

        public UiPalette Palette = UiPalette.Build(false, AccentKind.FishBlue);

        protected AnimControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.White;
        }

        public Color Surface
        {
            get { return BackColor; }
            set
            {
                if (BackColor != value)
                {
                    BackColor = value;
                    Invalidate();
                }
            }
        }

        public virtual void ApplyPalette(UiPalette palette)
        {
            if (palette == null)
            {
                return;
            }
            Palette = palette;
            Invalidate();
        }

        /// <summary>
        /// 先铺一层不透明底色。自绘控件必须自己清屏，
        /// 否则会留下上一次绘制（甚至别的控件）的像素。
        /// </summary>
        protected void PaintSurface(Graphics g)
        {
            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
        }

        protected void KickAnimation()
        {
            AnimBus.Kick();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AnimBus.Register(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            AnimBus.Unregister(this);
            base.OnHandleDestroyed(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Hovering = true;
            KickAnimation();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Hovering = false;
            Pressing = false;
            KickAnimation();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Pressing = true;
                KickAnimation();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Pressing = false;
            KickAnimation();
            base.OnMouseUp(e);
        }

        public virtual bool StepAnimation()
        {
            bool busy = false;
            float targetHover = Hovering || Focused ? 1f : 0f;
            if (Math.Abs(Hover - targetHover) > 0.01f)
            {
                Hover += (targetHover - Hover) * 0.26f;
                if (Math.Abs(Hover - targetHover) <= 0.012f)
                {
                    Hover = targetHover;
                }
                busy = true;
            }
            float targetPress = Pressing ? 1f : 0f;
            if (Math.Abs(Press - targetPress) > 0.01f)
            {
                Press += (targetPress - Press) * 0.38f;
                if (Math.Abs(Press - targetPress) <= 0.012f)
                {
                    Press = targetPress;
                }
                busy = true;
            }
            return busy;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            KickAnimation();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            KickAnimation();
            base.OnLostFocus(e);
        }
    }

    /// <summary>圆角卡片容器：只负责画底板和描边。</summary>
    internal sealed class SoftCard : Panel
    {
        public Color FillColor = Color.White;
        public Color LineColor = Color.FromArgb(225, 230, 238);
        public int Radius = 14;
        public bool ShowLine = true;

        public SoftCard()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.White;
        }

        public void ApplyColors(Color fill, Color line)
        {
            FillColor = fill;
            LineColor = line;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(BackColor))
            {
                g.FillRectangle(back, ClientRectangle);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = UiDraw.Rounded(rect, UiMetrics.SF(Radius)))
            {
                using (SolidBrush brush = new SolidBrush(FillColor))
                {
                    g.FillPath(brush, path);
                }
                if (ShowLine)
                {
                    using (Pen pen = new Pen(LineColor))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }
    }

    internal enum SoftButtonStyle
    {
        Primary = 0,
        Secondary = 1,
        Ghost = 2,
        Danger = 3
    }

    internal sealed class SoftButton : AnimControl
    {
        public SoftButtonStyle Style = SoftButtonStyle.Secondary;
        public int Radius = 10;

        public SoftButton()
        {
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9.5f);
            TabStop = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PaintSurface(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            float hover = Hover;
            float press = Press;
            Color fill;
            Color fore;
            Color border = Color.Transparent;
            bool drawBorder = false;

            if (Style == SoftButtonStyle.Primary)
            {
                fill = UiDraw.Blend(Palette.Accent, Palette.AccentHover, hover);
                fill = UiDraw.Blend(fill, Palette.AccentPress, press * 0.55f);
                fore = Palette.OnAccent;
            }
            else if (Style == SoftButtonStyle.Secondary)
            {
                fill = UiDraw.Blend(Palette.SurfaceAlt, Palette.SurfaceAlt, hover);
                fill = UiDraw.Blend(fill, Palette.BorderStrong, hover * 0.18f);
                fill = UiDraw.Blend(fill, Palette.BorderStrong, press * 0.18f);
                fore = Palette.Text;
                border = UiDraw.Blend(Palette.Border, Palette.BorderStrong, hover);
                drawBorder = true;
            }
            else if (Style == SoftButtonStyle.Danger)
            {
                fill = UiDraw.Blend(Surface, Palette.Danger, 0.10f * hover + 0.16f * press);
                fore = UiDraw.Blend(Palette.Danger, Palette.Danger, 0f);
                border = UiDraw.Blend(Palette.Border, Palette.Danger, 0.35f * hover);
                drawBorder = true;
            }
            else
            {
                fill = UiDraw.Blend(Surface, Palette.SurfaceAlt, hover);
                fore = Palette.Muted;
                drawBorder = false;
            }

            if (!Enabled)
            {
                fill = UiDraw.Blend(fill, Surface, 0.55f);
                fore = Palette.Muted;
            }

            using (GraphicsPath path = UiDraw.Rounded(rect, UiMetrics.SF(Radius)))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
                if (drawBorder)
                {
                    using (Pen pen = new Pen(border))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            Rectangle textRect = new Rectangle(6, 0, Width - 12, Height);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                fore,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            if (Focused && ShowFocusCues)
            {
                Rectangle ring = new Rectangle(2, 2, Width - 5, Height - 5);
                using (GraphicsPath path = UiDraw.Rounded(
                    ring, Math.Max(UiMetrics.SF(4), UiMetrics.SF(Radius) - 2)))
                {
                    using (Pen pen = new Pen(UiDraw.WithAlpha(Palette.Accent, 150), 1.4f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }
    }

    internal enum SoftIconKind
    {
        Settings = 0,
        Download = 1
    }

    internal sealed class SoftIconButton : AnimControl
    {
        public SoftIconKind Kind = SoftIconKind.Settings;
        public bool Active;

        public SoftIconButton()
        {
            Cursor = Cursors.Hand;
            Size = new Size(36, 36);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PaintSurface(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Active
                ? Palette.AccentSoft
                : UiDraw.Blend(Surface, Palette.SurfaceAlt, Hover);
            using (GraphicsPath path = UiDraw.Rounded(
                new Rectangle(0, 0, Width - 1, Height - 1), 10))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
            }

            Color ink = Active ? Palette.Accent : Palette.Muted;
            if (Hover > 0.4f || Focused)
            {
                ink = Active ? Palette.Accent : Palette.Text;
            }
            DrawIcon(g, Kind, Width / 2f, Height / 2f, ink, Surface);
        }

        internal static void DrawIcon(Graphics g, SoftIconKind kind, float cx, float cy, Color ink, Color back)
        {
            using (Pen pen = new Pen(ink, 1.7f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                if (kind == SoftIconKind.Download)
                {
                    g.DrawLine(pen, cx, cy - 8f, cx, cy + 2f);
                    g.DrawLine(pen, cx - 4.4f, cy - 1.8f, cx, cy + 2.4f);
                    g.DrawLine(pen, cx + 4.4f, cy - 1.8f, cx, cy + 2.4f);
                    GraphicsPath tray = new GraphicsPath();
                    tray.AddLine(cx - 8f, cy + 5f, cx - 8f, cy + 8f);
                    tray.AddLine(cx - 8f, cy + 8f, cx + 8f, cy + 8f);
                    tray.AddLine(cx + 8f, cy + 8f, cx + 8f, cy + 5f);
                    g.DrawPath(pen, tray);
                    tray.Dispose();
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float y = cy - 6f + i * 6f;
                        g.DrawLine(pen, cx - 8f, y, cx + 8f, y);
                        float knobX = i == 0 ? cx + 3f : (i == 1 ? cx - 3.5f : cx + 1f);
                        using (SolidBrush brush = new SolidBrush(ink))
                        {
                            g.FillEllipse(brush, knobX - 2.2f, y - 2.2f, 4.4f, 4.4f);
                        }
                        using (SolidBrush brush = new SolidBrush(back))
                        {
                            g.FillEllipse(brush, knobX - 0.9f, y - 0.9f, 1.8f, 1.8f);
                        }
                    }
                }
            }
        }
    }

    /// <summary>左侧导航栏的图标按钮。</summary>
    internal sealed class RailButton : AnimControl
    {
        public SoftIconKind Kind = SoftIconKind.Download;
        public bool Active;

        public RailButton()
        {
            Cursor = Cursors.Hand;
            Size = new Size(46, 46);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PaintSurface(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Active
                ? Palette.AccentSoft
                : UiDraw.Blend(Surface, Palette.RailHover, Hover);
            using (GraphicsPath path = UiDraw.Rounded(
                new Rectangle(0, 0, Width - 1, Height - 1), 13))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
            }

            Color ink = Active ? Palette.Accent : Palette.Muted;
            if (!Active && Hover > 0.35f)
            {
                ink = Palette.Text;
            }
            SoftIconButton.DrawIcon(g, Kind, Width / 2f, Height / 2f, ink, Surface);
        }
    }

    /// <summary>设置页左侧的分类项。</summary>
    internal sealed class SideItem : AnimControl
    {
        public bool Active;

        public SideItem()
        {
            Cursor = Cursors.Hand;
            Height = 40;
            Font = new Font("Microsoft YaHei UI", 9.5f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PaintSurface(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Active
                ? Palette.AccentSoft
                : UiDraw.Blend(Surface, Palette.SurfaceAlt, Hover);
            using (GraphicsPath path = UiDraw.Rounded(
                new Rectangle(0, 0, Width - 1, Height - 1), 10))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
            }

            if (Active)
            {
                Rectangle bar = new Rectangle(0, Height / 2 - UiMetrics.S(8), UiMetrics.S(3), UiMetrics.S(16));
                using (GraphicsPath path = UiDraw.Rounded(bar, 1.5f))
                {
                    using (SolidBrush brush = new SolidBrush(Palette.Accent))
                    {
                        g.FillPath(brush, path);
                    }
                }
            }

            Color fore = Active ? Palette.Accent : Palette.Text;
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                new Rectangle(UiMetrics.S(18), 0, Width - UiMetrics.S(24), Height),
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class SoftCheckBox : CheckBox, IAnimated
    {
        private float checkT;
        private bool target;
        public UiPalette Palette = UiPalette.Build(false, AccentKind.FishBlue);

        public SoftCheckBox()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            AutoSize = false;
            Height = 22;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Color.White;
        }

        public Color Surface
        {
            get { return BackColor; }
            set
            {
                if (BackColor != value)
                {
                    BackColor = value;
                    Invalidate();
                }
            }
        }

        /// <summary>按当前文字与字体收缩宽度（语言切换后重新排版用）。</summary>
        public void AutoFit()
        {
            int textWidth = TextRenderer.MeasureText(Text, Font).Width;
            Width = UiMetrics.S(29) + textWidth + UiMetrics.S(6);
            Invalidate();
        }

        public void ApplyPalette(UiPalette palette)
        {
            if (palette == null)
            {
                return;
            }
            Palette = palette;
            Invalidate();
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            target = Checked;
            AnimBus.Kick();
            base.OnCheckedChanged(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AnimBus.Register(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            AnimBus.Unregister(this);
            base.OnHandleDestroyed(e);
        }

        /// <summary>勾选标记的描画进度缓动。</summary>
        public bool StepAnimation()
        {
            float targetT = Checked ? 1f : 0f;
            if (Math.Abs(checkT - targetT) > 0.01f)
            {
                checkT += (targetT - checkT) * 0.3f;
                if (Math.Abs(checkT - targetT) <= 0.015f)
                {
                    checkT = targetT;
                }
                return true;
            }
            checkT = targetT;
            return false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (target != Checked)
            {
                target = Checked;
            }
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(BackColor))
            {
                g.FillRectangle(back, ClientRectangle);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int box = UiMetrics.S(18);
            int top = (Height - box) / 2;
            Rectangle rect = new Rectangle(UiMetrics.S(1), top, box, box);
            float t = UiDraw.EaseOut(checkT);

            Color fill;
            if (Checked)
            {
                fill = Palette.Accent;
            }
            else
            {
                fill = UiDraw.Blend(Surface, Palette.SurfaceAlt, 0.35f);
            }
            using (GraphicsPath path = UiDraw.Rounded(rect, UiMetrics.SF(5.5f)))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
                Color line = Checked
                    ? UiDraw.Blend(Palette.Accent, Palette.AccentPress, 0.2f)
                    : Palette.BorderStrong;
                using (Pen pen = new Pen(line, UiMetrics.SF(1.4f)))
                {
                    g.DrawPath(pen, path);
                }
            }

            if (t > 0.02f)
            {
                using (Pen pen = new Pen(
                    UiDraw.WithAlpha(Color.White, (int)(255 * t)), UiMetrics.SF(2f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    PointF p1 = new PointF(
                        rect.Left + UiMetrics.SF(4.6f), rect.Top + UiMetrics.SF(9.2f));
                    PointF p2 = new PointF(
                        rect.Left + UiMetrics.SF(7.8f), rect.Top + UiMetrics.SF(12.4f));
                    PointF p3 = new PointF(
                        rect.Left + UiMetrics.SF(13.4f), rect.Top + UiMetrics.SF(5.8f));
                    if (t < 0.5f)
                    {
                        float u = t / 0.5f;
                        g.DrawLine(pen, p1, new PointF(
                            p1.X + (p2.X - p1.X) * u,
                            p1.Y + (p2.Y - p1.Y) * u));
                    }
                    else
                    {
                        float u = (t - 0.5f) / 0.5f;
                        g.DrawLine(pen, p1, p2);
                        g.DrawLine(pen, p2, new PointF(
                            p2.X + (p3.X - p2.X) * u,
                            p2.Y + (p3.Y - p2.Y) * u));
                    }
                }
            }

            Color fore = Enabled ? Palette.Text : Palette.Muted;
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                new Rectangle(
                    UiMetrics.S(27), 0,
                    Math.Max(UiMetrics.S(10), Width - UiMetrics.S(29)), Height),
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>
    /// 圆角输入框容器：把原生 TextBox / NumericUpDown / RichTextBox 放进去，
    /// 由容器负责背景、描边和聚焦高亮。
    /// </summary>
    internal sealed class SoftField : Control
    {
        public Control Inner;
        public Color Fill = Color.White;
        public Color Line = Color.FromArgb(225, 230, 238);
        public Color LineActive = Color.FromArgb(46, 124, 246);
        public int Radius = 9;
        public bool StretchInner;
        public Padding InnerPadding = UiMetrics.P(10, 6, 8, 6);
        private bool innerActive;

        public SoftField(Control inner)
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.White;
            Inner = inner;
            Controls.Add(inner);
            inner.Enter += delegate
            {
                innerActive = true;
                Invalidate();
            };
            inner.Leave += delegate
            {
                innerActive = false;
                Invalidate();
            };
        }

        public void ApplyColors(Color fill, Color line, Color lineActive)
        {
            Fill = fill;
            Line = line;
            LineActive = lineActive;
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Inner == null)
            {
                return;
            }
            int left = InnerPadding.Left;
            int right = InnerPadding.Right;
            int width = Math.Max(10, Width - left - right);
            if (StretchInner)
            {
                Inner.SetBounds(
                    left,
                    InnerPadding.Top,
                    width,
                    Math.Max(10, Height - InnerPadding.Top - InnerPadding.Bottom));
            }
            else
            {
                int height = Inner.Height;
                int top = (Height - height) / 2;
                if (top < InnerPadding.Top)
                {
                    top = InnerPadding.Top;
                }
                Inner.SetBounds(left, top, width, height);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(BackColor))
            {
                g.FillRectangle(back, ClientRectangle);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color line = innerActive ? LineActive : Line;
            float width = innerActive ? 1.6f : 1f;
            using (GraphicsPath path = UiDraw.Rounded(rect, UiMetrics.SF(Radius)))
            {
                using (SolidBrush brush = new SolidBrush(Fill))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(line, width))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    /// <summary>三档外观模式开关：浅色 / 深色 / 跟随系统，滑块带缓动。</summary>
    internal sealed class SegmentedSwitch : AnimControl
    {
        private string[] labels = new string[] { "浅色", "深色", "跟随系统" };
        private readonly float[] fades = new float[3];
        private int selectedIndex;
        private float knob;

        public event EventHandler SelectedIndexChanged;

        public SegmentedSwitch()
        {
            Cursor = Cursors.Hand;
            Height = 44;
            TabStop = true;
            Font = new Font("Microsoft YaHei UI", 9.5f);
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
        }

        public int Count
        {
            get { return labels.Length; }
        }

        /// <summary>设置档位数量与标签（用于两档 FFmpeg 选择、语言切换）。</summary>
        public void SetLabels(string[] items, bool keepSelection)
        {
            if (items == null || items.Length == 0)
            {
                return;
            }
            labels = items;
            if (!keepSelection && selectedIndex >= labels.Length)
            {
                selectedIndex = labels.Length - 1;
            }
            if (knob > labels.Length - 1)
            {
                knob = labels.Length - 1;
            }
            AnimBus.Kick();
            Invalidate();
        }

        public void SetSelected(int index, bool animate)
        {
            if (index < 0)
            {
                index = 0;
            }
            if (index >= labels.Length)
            {
                index = labels.Length - 1;
            }
            bool changed = index != selectedIndex;
            selectedIndex = index;
            if (!animate)
            {
                knob = index;
                for (int i = 0; i < fades.Length; i++)
                {
                    fades[i] = i == index ? 1f : 0f;
                }
            }
            AnimBus.Kick();
            Invalidate();
            if (changed && SelectedIndexChanged != null)
            {
                SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public override bool StepAnimation()
        {
            bool busy = base.StepAnimation();
            float diff = selectedIndex - knob;
            if (Math.Abs(diff) > 0.004f)
            {
                knob += diff * 0.24f;
                if (Math.Abs(selectedIndex - knob) <= 0.004f)
                {
                    knob = selectedIndex;
                }
                busy = true;
            }
            for (int i = 0; i < fades.Length; i++)
            {
                float target = i == selectedIndex ? 1f : 0f;
                if (Math.Abs(fades[i] - target) > 0.02f)
                {
                    fades[i] += (target - fades[i]) * 0.3f;
                    busy = true;
                }
                else
                {
                    fades[i] = target;
                }
            }
            return busy;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PaintSurface(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = UiDraw.Rounded(track, Height / 2f))
            {
                using (SolidBrush brush = new SolidBrush(Palette.SurfaceAlt))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(Palette.Border))
                {
                    g.DrawPath(pen, path);
                }
            }

            float segment = (float)Width / labels.Length;
            float pad = UiMetrics.SF(3f);
            float knobWidth = segment - UiMetrics.SF(6f);
            float knobX = pad + knob * segment;
            RectangleF knobRect = new RectangleF(
                knobX, UiMetrics.SF(4f), knobWidth, Height - UiMetrics.SF(8f));

            for (int layer = 3; layer >= 1; layer--)
            {
                RectangleF shadow = knobRect;
                shadow.Y += layer * 0.8f;
                shadow.Inflate(layer * 0.5f, 0f);
                using (GraphicsPath path = UiDraw.Rounded(shadow, shadow.Height / 2f))
                {
                    using (SolidBrush brush = new SolidBrush(
                        UiDraw.WithAlpha(Color.Black, Palette.Dark ? 26 : 10)))
                    {
                        g.FillPath(brush, path);
                    }
                }
            }

            Color knobFill = Palette.Dark
                ? Color.FromArgb(58, 64, 73)
                : Color.White;
            using (GraphicsPath path = UiDraw.Rounded(knobRect, knobRect.Height / 2f))
            {
                using (SolidBrush brush = new SolidBrush(knobFill))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(Palette.Dark
                    ? UiDraw.WithAlpha(Color.White, 18)
                    : UiDraw.WithAlpha(Palette.Border, 220)))
                {
                    g.DrawPath(pen, path);
                }
            }

            using (Font activeFont = new Font(Font, FontStyle.Bold))
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    RectangleF cell = new RectangleF(
                        i * segment, 0f, segment, Height);
                    Color color = UiDraw.Blend(Palette.Muted, Palette.Text, fades[i]);
                    TextRenderer.DrawText(
                        g,
                        labels[i],
                        fades[i] > 0.55f ? activeFont : Font,
                        Rectangle.Round(cell),
                        color,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPrefix);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            SelectAt(e.X);
        }

        private void SelectAt(int x)
        {
            float segment = (float)Width / labels.Length;
            int index = (int)(x / segment);
            SetSelected(index, true);
            Focus();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left)
            {
                SetSelected(selectedIndex - 1, true);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                SetSelected(selectedIndex + 1, true);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }

    /// <summary>主题色选择卡：一个色点 + 名称 + 说明 + 选中勾。</summary>
    internal sealed class AccentSwatch : AnimControl
    {
        public AccentKind Kind = AccentKind.FishBlue;
        public string Title = "";
        public string Hint = "";
        private bool selected;
        private float sel;
        private readonly Font titleFont;
        private readonly Font hintFont;

        public event EventHandler Picked;

        public AccentSwatch()
        {
            Cursor = Cursors.Hand;
            Height = 66;
            TabStop = true;
            titleFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            hintFont = new Font("Microsoft YaHei UI", 8.25f);
        }

        public bool Selected
        {
            get { return selected; }
        }

        public void SetSelected(bool value, bool animate)
        {
            selected = value;
            if (!animate)
            {
                sel = value ? 1f : 0f;
            }
            AnimBus.Kick();
            Invalidate();
        }

        public override bool StepAnimation()
        {
            bool busy = base.StepAnimation();
            float target = selected ? 1f : 0f;
            if (Math.Abs(sel - target) > 0.02f)
            {
                sel += (target - sel) * 0.3f;
                busy = true;
            }
            else
            {
                sel = target;
            }
            return busy;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PaintSurface(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            float t = UiDraw.EaseOut(sel);
            Color fill = UiDraw.Blend(Palette.SurfaceAlt, Palette.AccentSoft, t);
            if (Hover > 0.01f && t < 0.5f)
            {
                fill = UiDraw.Blend(fill, Palette.SurfaceAlt, Hover * 0.4f);
            }
            Color line = UiDraw.Blend(Palette.Border, Palette.Accent, t);

            using (GraphicsPath path = UiDraw.Rounded(rect, 12))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(line, t > 0.5f ? 1.6f : 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            Color accent = UiPalette.AccentBase(Kind);
            Rectangle dot = new Rectangle(16, Height / 2 - 11, 22, 22);
            using (SolidBrush brush = new SolidBrush(accent))
            {
                g.FillEllipse(brush, dot);
            }
            using (Pen pen = new Pen(UiDraw.WithAlpha(Color.White, 90), 1.2f))
            {
                g.DrawEllipse(pen, dot);
            }

            TextRenderer.DrawText(
                g,
                Title,
                titleFont,
                new Rectangle(50, 14, Width - 100, 20),
                Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(
                g,
                Hint,
                hintFont,
                new Rectangle(50, 34, Width - 100, 18),
                Palette.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (t > 0.02f)
            {
                Rectangle check = new Rectangle(Width - 40, Height / 2 - 9, 18, 18);
                using (SolidBrush brush = new SolidBrush(
                    UiDraw.WithAlpha(Palette.Accent, (int)(255 * t))))
                {
                    g.FillEllipse(brush, check);
                }
                using (Pen pen = new Pen(
                    UiDraw.WithAlpha(Color.White, (int)(255 * t)), 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen,
                        check.Left + 4.6f, check.Top + 9.2f,
                        check.Left + 7.6f, check.Top + 12.2f);
                    g.DrawLine(pen,
                        check.Left + 7.6f, check.Top + 12.2f,
                        check.Left + 13.4f, check.Top + 5.8f);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Picked != null)
            {
                Picked(this, EventArgs.Empty);
            }
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                if (Picked != null)
                {
                    Picked(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }

    /// <summary>圆角进度条，数值变化时做缓动填充。</summary>
    internal sealed class SoftBar : AnimControl
    {
        private int value;
        private float shown;

        public SoftBar()
        {
            Height = 8;
        }

        public int Value
        {
            get { return value; }
            set
            {
                int next = value;
                if (next < 0)
                {
                    next = 0;
                }
                if (next > 100)
                {
                    next = 100;
                }
                if (value != 0 || next != this.value)
                {
                    this.value = next;
                    AnimBus.Kick();
                    Invalidate();
                }
            }
        }

        public override bool StepAnimation()
        {
            bool busy = base.StepAnimation();
            float diff = value - shown;
            if (Math.Abs(diff) > 0.25f)
            {
                shown += diff * 0.22f;
                busy = true;
            }
            else
            {
                shown = value;
            }
            return busy;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PaintSurface(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int h = Height;
            using (GraphicsPath track = UiDraw.Rounded(
                new Rectangle(0, 0, Width, h), h / 2f))
            {
                using (SolidBrush brush = new SolidBrush(Palette.Border))
                {
                    g.FillPath(brush, track);
                }
            }
            float ratio = Math.Max(0f, Math.Min(100f, shown)) / 100f;
            if (ratio <= 0f)
            {
                return;
            }
            int fillWidth = (int)Math.Max(h, Width * ratio);
            using (GraphicsPath fill = UiDraw.Rounded(
                new Rectangle(0, 0, fillWidth, h), h / 2f))
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(1, fillWidth), h),
                    Palette.Accent,
                    Palette.AccentHover,
                    LinearGradientMode.Horizontal))
                {
                    g.FillPath(brush, fill);
                }
            }
        }
    }

    /// <summary>
    /// 纯文本数字框：替代原生 NumericUpDown。
    /// 原生微调按钮在深色下会露白块、隐藏后又留下黑块，这里自己实现取值与范围。
    /// </summary>
    internal sealed class SoftNumber : TextBox
    {
        private decimal minimum;
        private decimal maximum = 100m;
        private decimal current;
        private bool updating;

        public SoftNumber()
        {
            BorderStyle = BorderStyle.None;
            Font = new Font("Microsoft YaHei UI", 9.5f);
        }

        public decimal Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                if (current < minimum)
                {
                    Value = minimum;
                }
            }
        }

        public decimal Maximum
        {
            get { return maximum; }
            set
            {
                maximum = value;
                if (current > maximum)
                {
                    Value = maximum;
                }
            }
        }

        public decimal Value
        {
            get { return current; }
            set
            {
                decimal next = value;
                if (next < minimum)
                {
                    next = minimum;
                }
                if (next > maximum)
                {
                    next = maximum;
                }
                current = next;
                string text = next.ToString("0");
                if (Text != text)
                {
                    updating = true;
                    Text = text;
                    updating = false;
                }
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (updating)
            {
                return;
            }
            decimal parsed;
            if (decimal.TryParse(Text.Trim(), out parsed))
            {
                if (parsed < minimum)
                {
                    parsed = minimum;
                }
                if (parsed > maximum)
                {
                    parsed = maximum;
                }
                current = parsed;
            }
        }

        protected override void OnLostFocus(EventArgs e)
        {
            // 失焦时把内容规范成合法数字，避免留下空值
            Value = current;
            base.OnLostFocus(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
            base.OnKeyPress(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                Value = current + 1m;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                Value = current - 1m;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Value = current + (e.Delta > 0 ? 1m : -1m);
        }
    }

    /// <summary>状态圆点（沿用原代码里 stateBox.BackColor 的用法）。</summary>
    internal sealed class DotPanel : Control
    {
        public Color Surface = Color.White;

        public DotPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Size = new Size(10, 10);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(Surface))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                g.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
