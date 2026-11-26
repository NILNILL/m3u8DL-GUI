// 窗口抓取：用 PrintWindow(PW_RENDERFULLCONTENT) 抓取被遮挡窗口的真实画面。
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

internal static class CaptureWindow
{
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string cls, string title);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static void Main(string[] args)
    {
        SetProcessDPIAware();
        string name = args.Length > 0 ? args[0] : "capture.png";

        IntPtr hwnd = IntPtr.Zero;
        for (int i = 0; i < 40 && hwnd == IntPtr.Zero; i++)
        {
            hwnd = FindWindow(null, "m3u8DL-GUI");
            if (hwnd == IntPtr.Zero)
            {
                Thread.Sleep(200);
            }
        }
        if (hwnd == IntPtr.Zero)
        {
            Console.WriteLine("window not found");
            return;
        }

        Thread.Sleep(1200);

        RECT rect;
        GetWindowRect(hwnd, out rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        using (Bitmap bitmap = new Bitmap(width, height))
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = g.GetHdc();
                bool ok = PrintWindow(hwnd, hdc, 2);
                g.ReleaseHdc(hdc);
                Console.WriteLine("PrintWindow=" + ok);
            }
            bitmap.Save(name, ImageFormat.Png);
        }
        Console.WriteLine("saved " + name + " " + width + "x" + height);
    }
}
