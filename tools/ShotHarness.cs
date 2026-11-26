// 离屏截图工具：输出各主题状态 PNG 供人工比对。仅开发期使用，不随软件发布。
// 编译：csc /target:exe /main:ShotHarness ...（见 AGENTS.md）
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using M3u8DlGui;

internal static class ShotHarness
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static void Main(string[] args)
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string outDir = args.Length > 0 ? args[0] : "shots";
        Directory.CreateDirectory(outDir);

        // 截图会触发设置持久化，运行前后备份并还原用户的真实设置。
        string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "m3u8DL-GUI", "settings.ini");
        string backup = File.Exists(settingsPath)
            ? File.ReadAllText(settingsPath)
            : null;

        Type type = typeof(MainForm);
        MainForm form = new MainForm();
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(0, 0);
        form.ShowInTaskbar = false;
        form.Show();
        Application.DoEvents();

        Shot(form, type, Path.Combine(outDir, "01-light-download.png"),
            ThemeMode.Light, AccentKind.FishBlue, 0);
        Shot(form, type, Path.Combine(outDir, "02-dark-download.png"),
            ThemeMode.Dark, AccentKind.FishBlue, 0);
        Shot(form, type, Path.Combine(outDir, "03-light-settings.png"),
            ThemeMode.Light, AccentKind.FishBlue, 1);
        Shot(form, type, Path.Combine(outDir, "04-dark-settings-claude.png"),
            ThemeMode.Dark, AccentKind.ClaudeOrange, 1);
        Shot(form, type, Path.Combine(outDir, "05-light-settings-claude.png"),
            ThemeMode.Light, AccentKind.ClaudeOrange, 1);

        form.Hide();
        form.Dispose();

        try
        {
            if (backup == null)
            {
                if (File.Exists(settingsPath))
                {
                    File.Delete(settingsPath);
                }
            }
            else
            {
                File.WriteAllText(settingsPath, backup);
            }
        }
        catch
        {
        }

        Console.WriteLine("done -> " + Path.GetFullPath(outDir));
    }

    private static void Shot(
        MainForm form,
        Type type,
        string path,
        ThemeMode mode,
        AccentKind accent,
        int page)
    {
        SetField(type, form, "themeMode", mode);
        SetField(type, form, "accentKind", accent);
        SetField(type, form, "pageT", page == 0 ? 0f : 1f);
        SetField(type, form, "pageTarget", page);
        Invoke(type, form, "ApplyTheme");
        Invoke(type, form, "LayoutPages");

        RailButton navDownload = (RailButton)GetField(type, form, "navDownload");
        RailButton navSettings = (RailButton)GetField(type, form, "navSettings");
        navDownload.Active = page == 0;
        navDownload.Invalidate();
        navSettings.Active = page == 1;
        navSettings.Invalidate();

        form.PerformLayout();
        Application.DoEvents();
        System.Threading.Thread.Sleep(320);
        form.Refresh();
        Application.DoEvents();

        using (Bitmap bitmap = new Bitmap(
            form.ClientSize.Width, form.ClientSize.Height))
        {
            form.DrawToBitmap(bitmap,
                new Rectangle(0, 0, form.ClientSize.Width, form.ClientSize.Height));
            bitmap.Save(path, ImageFormat.Png);
        }
        Console.WriteLine("saved " + path);
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

    private static void Invoke(Type type, object target, string name)
    {
        MethodInfo method = type.GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method != null)
        {
            method.Invoke(target, null);
        }
    }
}
