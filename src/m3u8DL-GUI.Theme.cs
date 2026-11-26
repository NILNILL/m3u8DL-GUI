using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace M3u8DlGui
{
    internal enum ThemeMode
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    internal enum AccentKind
    {
        FishBlue = 0,
        ClaudeOrange = 1
    }

    /// <summary>
    /// 一套完整的界面配色。明暗两种基底 × 两种强调色，共 4 种组合。
    /// </summary>
    internal sealed class UiPalette
    {
        public bool Dark;

        public Color Window;
        public Color Surface;
        public Color SurfaceAlt;
        public Color Input;
        public Color Text;
        public Color Muted;
        public Color Border;
        public Color BorderStrong;
        public Color Hairline;

        public Color Accent;
        public Color AccentHover;
        public Color AccentPress;
        public Color AccentSoft;
        public Color OnAccent;

        public Color Rail;
        public Color RailHover;
        public Color RailActive;

        public Color LogBack;
        public Color LogText;

        public Color Success;
        public Color Warning;
        public Color Danger;

        public static UiPalette Build(bool dark, AccentKind accent)
        {
            UiPalette p = new UiPalette();
            p.Dark = dark;
            p.Accent = AccentBase(accent);
            p.AccentHover = AccentHoverColor(accent);
            p.AccentPress = AccentPressColor(accent);
            p.AccentSoft = AccentSoftColor(accent, dark);
            p.OnAccent = Color.White;

            if (dark)
            {
                p.Window = Color.FromArgb(23, 26, 31);
                p.Surface = Color.FromArgb(33, 37, 43);
                p.SurfaceAlt = Color.FromArgb(40, 45, 52);
                p.Input = Color.FromArgb(38, 43, 50);
                p.Text = Color.FromArgb(232, 235, 239);
                p.Muted = Color.FromArgb(152, 162, 174);
                p.Border = Color.FromArgb(50, 57, 66);
                p.BorderStrong = Color.FromArgb(65, 74, 85);
                p.Hairline = Color.FromArgb(42, 48, 55);

                p.Rail = Color.FromArgb(27, 31, 37);
                p.RailHover = Color.FromArgb(38, 43, 50);
                p.RailActive = p.AccentSoft;

                p.Success = Color.FromArgb(87, 198, 143);
                p.Warning = Color.FromArgb(226, 173, 85);
                p.Danger = Color.FromArgb(240, 127, 124);
            }
            else
            {
                p.Window = Color.FromArgb(243, 245, 249);
                p.Surface = Color.White;
                p.SurfaceAlt = Color.FromArgb(241, 244, 248);
                p.Input = Color.White;
                p.Text = Color.FromArgb(26, 30, 36);
                p.Muted = Color.FromArgb(102, 112, 133);
                p.Border = Color.FromArgb(225, 230, 238);
                p.BorderStrong = Color.FromArgb(203, 212, 225);
                p.Hairline = Color.FromArgb(230, 234, 240);

                p.Rail = Color.FromArgb(251, 252, 254);
                p.RailHover = Color.FromArgb(241, 244, 248);
                p.RailActive = p.AccentSoft;

                p.Success = Color.FromArgb(46, 158, 104);
                p.Warning = Color.FromArgb(201, 138, 32);
                p.Danger = Color.FromArgb(220, 90, 87);
            }

            p.LogBack = Color.FromArgb(19, 21, 25);
            p.LogText = Color.FromArgb(210, 215, 221);
            return p;
        }

        public static Color AccentBase(AccentKind kind)
        {
            if (kind == AccentKind.ClaudeOrange)
            {
                return Color.FromArgb(217, 119, 87);
            }
            return Color.FromArgb(46, 124, 246);
        }

        public static Color AccentHoverColor(AccentKind kind)
        {
            if (kind == AccentKind.ClaudeOrange)
            {
                return Color.FromArgb(226, 140, 112);
            }
            return Color.FromArgb(74, 143, 250);
        }

        public static Color AccentPressColor(AccentKind kind)
        {
            if (kind == AccentKind.ClaudeOrange)
            {
                return Color.FromArgb(191, 99, 67);
            }
            return Color.FromArgb(31, 99, 210);
        }

        public static Color AccentSoftColor(AccentKind kind, bool dark)
        {
            if (kind == AccentKind.ClaudeOrange)
            {
                return dark
                    ? Color.FromArgb(59, 38, 32)
                    : Color.FromArgb(251, 237, 230);
            }
            return dark
                ? Color.FromArgb(27, 44, 69)
                : Color.FromArgb(231, 240, 254);
        }

        public static string AccentName(AccentKind kind)
        {
            return kind == AccentKind.ClaudeOrange ? "Claude 橙" : "大肥鱼蓝";
        }

        public static string AccentHint(AccentKind kind)
        {
            return kind == AccentKind.ClaudeOrange
                ? "暖橙，偏柔和，适合长时间使用"
                : "明亮海蓝，默认配色";
        }
    }

    internal static class UiMetrics
    {
        /// <summary>系统 DPI / 96。界面按 96 DPI 设计，运行时统一放大。</summary>
        public static float Scale = 1f;

        public static int S(int value)
        {
            return (int)Math.Round(value * Scale, MidpointRounding.AwayFromZero);
        }

        public static float SF(float value)
        {
            return value * Scale;
        }

        public static Padding P(int all)
        {
            int v = S(all);
            return new Padding(v);
        }

        public static Padding P(int left, int top, int right, int bottom)
        {
            return new Padding(S(left), S(top), S(right), S(bottom));
        }

        public static Size SizeOf(int width, int height)
        {
            return new Size(S(width), S(height));
        }

        public static Point At(int x, int y)
        {
            return new Point(S(x), S(y));
        }
    }

    internal static class UiDraw
    {
        public static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0.5f)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            float d = radius * 2f;
            if (d > rect.Width)
            {
                d = rect.Width;
            }
            if (d > rect.Height)
            {
                d = rect.Height;
            }
            if (d <= 0f)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            path.AddArc(rect.Left, rect.Top, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        public static Color Blend(Color a, Color b, float t)
        {
            if (t <= 0f)
            {
                return a;
            }
            if (t >= 1f)
            {
                return b;
            }
            int r = (int)(a.R + (b.R - a.R) * t);
            int g = (int)(a.G + (b.G - a.G) * t);
            int bl = (int)(a.B + (b.B - a.B) * t);
            int al = (int)(a.A + (b.A - a.A) * t);
            return Color.FromArgb(Clamp(al), Clamp(r), Clamp(g), Clamp(bl));
        }

        public static int Clamp(int value)
        {
            if (value < 0)
            {
                return 0;
            }
            if (value > 255)
            {
                return 255;
            }
            return value;
        }

        public static Color Darken(Color c, float amount)
        {
            return Blend(c, Color.Black, amount);
        }

        public static Color Lighten(Color c, float amount)
        {
            return Blend(c, Color.White, amount);
        }

        /// <summary>三次缓出，适合「快速起步、柔和收尾」的位移。</summary>
        public static float EaseOut(float t)
        {
            if (t <= 0f)
            {
                return 0f;
            }
            if (t >= 1f)
            {
                return 1f;
            }
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseInOut(float t)
        {
            if (t <= 0f)
            {
                return 0f;
            }
            if (t >= 1f)
            {
                return 1f;
            }
            if (t < 0.5f)
            {
                return 2f * t * t;
            }
            float u = -2f * t + 2f;
            return 1f - u * u / 2f;
        }

        public static Color WithAlpha(Color c, int alpha)
        {
            return Color.FromArgb(Clamp(alpha), c.R, c.G, c.B);
        }
    }
}
