using System;
using System.Net;
using System.Text.RegularExpressions;

namespace M3u8DlGui
{
    internal sealed class UpdateInfo
    {
        public bool Ok;
        public bool HasNewer;
        public bool NoRelease;
        public string Tag = "";
        public string Url = "";
        public string Error = "";
    }

    /// <summary>
    /// 检查 GitHub 上的最新版本。
    /// 走 /releases/latest 的跳转地址（.../releases/tag/1.2.0），
    /// 不用 API，避免未登录时的速率限制（60 次/小时）。
    /// </summary>
    internal static class UpdateChecker
    {
        private static readonly Regex RxTag =
            new Regex("/releases/tag/([^/?#]+)", RegexOptions.Compiled);

        public static UpdateInfo Fetch(string repoUrl, string currentVersion)
        {
            UpdateInfo info = new UpdateInfo();
            try
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                }
                catch
                {
                }

                string url = repoUrl.TrimEnd('/') + "/releases/latest";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "m3u8DL-GUI";
                request.AllowAutoRedirect = true;
                request.MaximumAutomaticRedirections = 5;
                request.Timeout = 12000;
                request.ReadWriteTimeout = 12000;

                using (HttpWebResponse response =
                    (HttpWebResponse)request.GetResponse())
                {
                    string finalUrl = response.ResponseUri == null
                        ? ""
                        : response.ResponseUri.ToString();
                    Match match = RxTag.Match(finalUrl);
                    if (match.Success)
                    {
                        info.Tag = Uri.UnescapeDataString(match.Groups[1].Value);
                        info.Url = finalUrl;
                        info.Ok = true;
                        info.HasNewer = Compare(info.Tag, currentVersion) > 0;
                    }
                    else
                    {
                        // 打开了 releases 页面但没有具体版本：说明还没发布
                        info.Ok = true;
                        info.NoRelease = true;
                    }
                }
            }
            catch (WebException webError)
            {
                HttpWebResponse response = webError.Response as HttpWebResponse;
                if (response != null &&
                    response.StatusCode == HttpStatusCode.NotFound)
                {
                    info.Ok = true;
                    info.NoRelease = true;
                }
                else
                {
                    info.Ok = false;
                    info.Error = webError.Message;
                }
            }
            catch (Exception ex)
            {
                info.Ok = false;
                info.Error = ex.Message;
            }
            return info;
        }

        /// <summary>按 x.y.z 比较版本，返回正数表示 left 更新。</summary>
        public static int Compare(string left, string right)
        {
            int[] a = Parse(left);
            int[] b = Parse(right);
            for (int i = 0; i < 3; i++)
            {
                if (a[i] != b[i])
                {
                    return a[i] > b[i] ? 1 : -1;
                }
            }
            return 0;
        }

        private static int[] Parse(string version)
        {
            int[] parts = new int[3];
            if (string.IsNullOrEmpty(version))
            {
                return parts;
            }
            string text = version.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1);
            }
            string[] pieces = text.Split('.', '-', '+');
            for (int i = 0; i < 3 && i < pieces.Length; i++)
            {
                int value;
                if (int.TryParse(pieces[i], out value))
                {
                    parts[i] = value;
                }
            }
            return parts;
        }
    }
}
