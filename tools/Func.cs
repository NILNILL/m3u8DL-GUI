// 功能测试：验证页面切换、主题切换与设置持久化。仅开发期使用。
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using M3u8DlGui;

internal static class Func
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    private static StreamWriter log;
    private static int failures;

    [STAThread]
    private static void Main()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        log = new StreamWriter(Path.Combine(baseDir, "func.log"), false);
        try
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Type type = typeof(MainForm);
            MainForm form = new MainForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(0, 0);
            form.ShowInTaskbar = false;
            form.Show();
            Pump(300);

            // 1) 打开设置页，等动画结束
            Invoke(type, form, "ShowPage", 1);
            Pump(700);
            object pageHost = GetField(type, form, "pageHost");
            object downloadPage = GetField(type, form, "downloadPage");
            object settingsPage = GetField(type, form, "settingsPage");
            int hostWidth = ((Control)pageHost).ClientSize.Width;
            Check("设置页停在视口内",
                ((Control)settingsPage).Left == 0 &&
                ((Control)downloadPage).Left == -hostWidth,
                "settings.Left=" + ((Control)settingsPage).Left +
                " download.Left=" + ((Control)downloadPage).Left);

            // 2) 切到深色（滑块索引 1）
            SegmentedSwitch seg = (SegmentedSwitch)GetField(type, form, "segMode");
            seg.SetSelected(1, true);
            Pump(500);
            Check("深色模式已生效", (bool)GetField(type, form, "currentDark") == true, "currentDark");
            Check("滑块位置跟随", seg.SelectedIndex == 1, "index=" + seg.SelectedIndex);

            // 3) 切到 Claude 橙
            Invoke(type, form, "SelectAccent", AccentKind.ClaudeOrange);
            Pump(400);
            object accent = GetField(type, form, "accentKind");
            Check("强调色已切换", accent.ToString() == "ClaudeOrange", accent.ToString());

            // 4) 设置文件内容
            string settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "m3u8DL-GUI", "settings.ini");
            Pump(200);
            Check("设置文件已写入", File.Exists(settingsPath), settingsPath);
            if (File.Exists(settingsPath))
            {
                string text = File.ReadAllText(settingsPath);
                Check("settings 记录 dark", text.Contains("theme=dark"), text.Replace("\r\n", " | "));
                Check("settings 记录 claude", text.Contains("accent=claude"), "accent");
            }

            // 5) 回到下载页
            Invoke(type, form, "ShowPage", 0);
            Pump(700);
            Check("返回下载页", ((Control)downloadPage).Left == 0,
                "download.Left=" + ((Control)downloadPage).Left);

            form.Hide();
            form.Dispose();

            // 6) 重新创建窗口，验证持久化
            MainForm second = new MainForm();
            Check("重启后沿用深色",
                (bool)GetField(type, second, "currentDark") == true, "dark");
            Check("重启后沿用 Claude 橙",
                GetField(type, second, "accentKind").ToString() == "ClaudeOrange", "accent");
            second.Dispose();

            log.WriteLine(failures == 0 ? "ALL PASS" : ("FAILURES: " + failures));
        }
        catch (Exception ex)
        {
            log.WriteLine("EXCEPTION: " + ex);
        }
        finally
        {
            log.Flush();
            log.Close();
        }
    }

    private static void Pump(int milliseconds)
    {
        DateTime end = DateTime.Now.AddMilliseconds(milliseconds);
        while (DateTime.Now < end)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(15);
        }
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (!ok)
        {
            failures++;
        }
        log.WriteLine((ok ? "PASS  " : "FAIL  ") + name + "  [" + detail + "]");
        log.Flush();
    }

    private static void SetField(Type type, object target, string name, object value)
    {
        FieldInfo field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }

    private static object GetField(Type type, object target, string name)
    {
        FieldInfo field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field == null ? null : field.GetValue(target);
    }

    private static void Invoke(Type type, object target, string name, params object[] args)
    {
        MethodInfo method = type.GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method != null)
        {
            method.Invoke(target, args);
        }
    }
}
