using System;
using System.Collections.Generic;
using System.Globalization;

namespace M3u8DlGui
{
    internal enum LangMode
    {
        System = 0,
        ZhHans = 1,
        ZhHant = 2,
        En = 3
    }

    /// <summary>
    /// 界面文案字典：以简体中文原文为键，提供繁体与英文。
    /// 这样静态控件可以在构建后统一翻译，不必在每个控件上挂 key。
    /// </summary>
    internal static class Lang
    {
        public static LangMode Setting = LangMode.System;
        public static LangMode Current = LangMode.ZhHans;

        private static readonly Dictionary<string, string[]> Map =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        static Lang()
        {
            // key（简体原文）, 繁体, English
            Add("视频下载器 · 支持 m3u8 / MPD / 直链", "視頻下載器 · 支援 m3u8 / MPD / 直鏈", "Video downloader · m3u8 / MPD / direct link");
            Add("窗口置顶", "視窗置頂", "Always on top");
            Add("下载", "下載", "Downloads");
            Add("设置", "設定", "Settings");
            Add("下载链接", "下載連結", "Download URL");
            Add("保存名称", "儲存名稱", "File name");
            Add("线程", "執行緒", "Threads");
            Add("限速KB/s", "限速KB/s", "Speed limit KB/s");
            Add("代理", "代理", "Proxy");
            Add("保存目录", "儲存目錄", "Save folder");
            Add("浏览", "瀏覽", "Browse");
            Add("打开", "開啟", "Open");
            Add("开始下载", "開始下載", "Start");
            Add("停止选中", "停止選中", "Stop");
            Add("删除选中", "刪除選中", "Remove");
            Add("清空已完成", "清空已完成", "Clear finished");
            Add("完成后删除临时分片文件", "完成後刪除暫存分片檔案", "Delete temp parts when done");
            Add("达到重连上限后继续下载", "達到重連上限後繼續下載", "Keep going after retry limit");
            Add("重连成功后重置次数", "重連成功後重置次數", "Reset retry count after reconnect");
            Add("任务列表", "任務列表", "Tasks");
            Add("名称", "名稱", "Name");
            Add("进度", "進度", "Progress");
            Add("速度", "速度", "Speed");
            Add("状态", "狀態", "Status");
            Add("地址", "位址", "URL");
            Add("下载进度", "下載進度", "Progress");
            Add("信息", "資訊", "Info");
            Add("成功", "成功", "Success");
            Add("警告", "警告", "Warning");
            Add("失败", "失敗", "Error");
            Add("主题", "主題", "Theme");
            Add("语言", "語言", "Language");
            Add("关于", "關於", "About");
            Add("选择强调色与外观模式，修改立即生效并自动保存。", "選擇強調色與外觀模式，修改立即生效並自動儲存。", "Pick an accent color and appearance mode. Changes apply instantly and are saved.");
            Add("主题色", "主題色", "Accent color");
            Add("大肥鱼蓝", "大肥魚藍", "Big Fish Blue");
            Add("明亮海蓝，默认配色", "明亮海藍，預設配色", "Bright ocean blue, the default");
            Add("Claude 橙", "Claude 橙", "Claude Orange");
            Add("暖橙，柔和护眼", "暖橙，柔和護眼", "Warm orange, easy on the eyes");
            Add("外观模式", "外觀模式", "Appearance");
            Add("浅色", "淺色", "Light");
            Add("深色", "深色", "Dark");
            Add("跟随系统", "跟隨系統", "System");
            Add("浅色适合白天，深色降低夜间亮度；跟随系统会随 Windows 的设置自动切换。", "淺色適合白天，深色降低夜間亮度；跟隨系統會隨 Windows 的設定自動切換。", "Light for daytime, dark for night. System follows your Windows setting.");
            Add("选择 FFmpeg 来源，下载时优先使用所选项。", "選擇 FFmpeg 來源，下載時優先使用所選項。", "Choose where FFmpeg comes from. The selected one is used first.");
            Add("使用软件目录", "使用軟體目錄", "App folder");
            Add("自定义目录", "自訂目錄", "Custom folder");
            Add("选择文件夹", "選擇資料夾", "Choose folder");
            Add("自动检测", "自動偵測", "Auto detect");
            Add("优先使用软件目录内的 ffmpeg.exe，绿色便携。", "優先使用軟體目錄內的 ffmpeg.exe，綠色可攜。", "Uses ffmpeg.exe next to the app. Portable and self-contained.");
            Add("指定一个包含 ffmpeg.exe 的文件夹。", "指定一個包含 ffmpeg.exe 的資料夾。", "Point to a folder that contains ffmpeg.exe.");
            Add("请选择 ffmpeg.exe 所在的文件夹", "請選擇 ffmpeg.exe 所在的資料夾", "Select the folder that contains ffmpeg.exe");
            Add("该文件夹里没有 ffmpeg.exe，请重新选择。", "該資料夾裡沒有 ffmpeg.exe，請重新選擇。", "No ffmpeg.exe in that folder. Please choose again.");
            Add("已在系统 PATH 中找到 ffmpeg", "已在系統 PATH 中找到 ffmpeg", "Found ffmpeg in the system PATH");
            Add("系统 PATH 中未找到 ffmpeg", "系統 PATH 中未找到 ffmpeg", "No ffmpeg found in the system PATH");
            Add("未设置", "未設定", "Not set");
            Add("语言与界面文字，切换后立即生效。", "語言與介面文字，切換後立即生效。", "Interface language. Applies immediately.");
            Add("简体中文", "简体中文", "简体中文");
            Add("繁體中文", "繁體中文", "繁體中文");
            Add("English", "English", "English");
            Add("下载引擎", "下載引擎", "Download engine");
            Add("N_m3u8DL-CLI-engine.exe（原作者 nilaoda，MIT 许可证）", "N_m3u8DL-CLI-engine.exe（原作者 nilaoda，MIT 授權）", "N_m3u8DL-CLI-engine.exe (by nilaoda, MIT license)");
            Add("本程序仅用于个人学习与技术研究，请勿用于下载或传播无权使用的内容。", "本程式僅用於個人學習與技術研究，請勿用於下載或傳播無權使用的內容。", "For personal study and research only. Do not download or share content you have no rights to.");
            Add("检查更新", "檢查更新", "Check for updates");
            Add("正在检查…", "正在檢查…", "Checking…");
            Add("已是最新版本", "已是最新版本", "You are up to date");
            Add("发现新版本", "發現新版本", "New version available");
            Add("打开下载页", "開啟下載頁", "Open download page");
            Add("检查失败，请稍后重试", "檢查失敗，請稍後重試", "Check failed, please try again later");
            Add("图形界面外壳", "圖形介面外殼", "GUI shell");
            Add("版本", "版本", "Version");
            Add("待下载", "待下載", "Waiting");
            Add("等待", "等待", "Waiting");
            Add("启动中", "啟動中", "Starting");
            Add("重连中", "重連中", "Reconnecting");
            Add("完成", "完成", "Done");
            Add("失败", "失敗", "Failed");
            Add("已停止", "已停止", "Stopped");
            Add("启动失败", "啟動失敗", "Start failed");
            Add("重连失败", "重連失敗", "Reconnect failed");
            Add("已删除", "已刪除", "Removed");
            Add("就绪", "就緒", "Ready");
            Add("提示", "提示", "Notice");
            Add("确认", "確認", "Confirm");
            Add("错误", "錯誤", "Error");
            Add("请输入下载链接。", "請輸入下載連結。", "Please enter a download URL.");
            Add("链接看起来不是 http(s) 地址，仍要尝试下载吗？", "連結看起來不是 http(s) 位址，仍要嘗試下載嗎？", "This does not look like an http(s) URL. Try anyway?");
            Add("确认退出", "確認退出", "Confirm exit");
            Add("仍有下载任务正在运行，确定要退出并停止全部任务吗？", "仍有下載任務正在執行，確定要退出並停止全部任務嗎？", "Downloads are still running. Exit and stop them all?");
            Add("启动下载进程失败:", "啟動下載進程失敗:", "Failed to start the download process:");
            Add("无法启动下载引擎：", "無法啟動下載引擎：", "Cannot start the download engine: ");
            Add("删除任务和文件", "刪除任務與檔案", "Remove task and files");
            Add("删除后无法恢复，是否继续？", "刪除後無法復原，是否繼續？", "This cannot be undone. Continue?");
            Add("选择视频保存文件夹", "選擇影片儲存資料夾", "Select the video save folder");
            Add("任务正在下载，停止后会删除任务及对应文件。", "任務正在下載，停止後會刪除任務及對應檔案。", "This task is downloading; stopping will delete it and its files.");
            Add("将从任务列表删除，并删除以下下载内容：", "將從任務列表刪除，並刪除以下下載內容：", "Will remove the task and delete these files:");
            Add("（当前没有检测到已生成的视频或分片文件）", "（目前沒有偵測到已產生的影片或分片檔案）", "(No video or segment files detected)");
            Add("下载中", "下載中", "Downloading");
            Add("主题色/外观", "主題色/外觀", "Theme");
            Add("FFmpeg 来源", "FFmpeg 來源", "FFmpeg source");
            Add("FFmpeg", "FFmpeg", "FFmpeg");
            Add("暂无发布版本", "暫無發布版本", "No release published yet");
            Add("当前版本", "目前版本", "Current version");
            Add("切换语言需要重新打开软件才能生效，是否立即重启？", "切換語言需要重新開啟軟體才能生效，是否立即重新啟動？", "Changing the language requires restarting the app. Restart now?");
            Add("请先停止正在进行的下载任务。", "請先停止正在進行的下載任務。", "Please stop the running downloads first.");
            Add("开发者", "開發者", "Developer");
            Add("引擎", "引擎", "Engine");
        }

        private static void Add(string zh, string zhHant, string en)
        {
            Map[zh] = new string[] { zhHant, en };
        }

        public static string Translate(string zh)
        {
            if (string.IsNullOrEmpty(zh))
            {
                return zh;
            }
            if (Current == LangMode.ZhHans)
            {
                return zh;
            }
            string[] pair;
            if (!Map.TryGetValue(zh, out pair))
            {
                return zh;
            }
            return Current == LangMode.En ? pair[1] : pair[0];
        }

        /// <summary>根据设置解析实际语言；System 走 Windows 显示语言。</summary>
        public static LangMode Resolve(LangMode setting)
        {
            if (setting != LangMode.System)
            {
                return setting;
            }
            try
            {
                string name = CultureInfo.CurrentUICulture.Name;
                if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    bool traditional =
                        name.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("TW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("HK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MO", StringComparison.OrdinalIgnoreCase) >= 0;
                    return traditional ? LangMode.ZhHant : LangMode.ZhHans;
                }
                return LangMode.En;
            }
            catch
            {
                return LangMode.ZhHans;
            }
        }

        /// <summary>任务状态值（内部仍保存简体，显示时翻译）。</summary>
        public static string Status(string status)
        {
            return Translate(status);
        }
    }
}
