using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace TouchpadGood
{
    internal sealed class UpdateCheckResult
    {
        public bool Success;
        public bool HasUpdate;
        public string LatestVersion;
        public string DownloadUrl;
        public string Error;
    }

    internal static class UpdateChecker
    {
        private static void EnsureTls12()
        {
            try
            {
                // SecurityProtocolType.Tls12 = 3072, Tls11 = 768 (safe on .NET 4.0+)
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768;
            }
            catch { }
        }

        private static string HttpGetJson(string url, int timeoutMs)
        {
            EnsureTls12();
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.UserAgent = AppMeta.AppName + "/" + AppMeta.Version;
            req.Accept = "application/vnd.github+json";
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public static UpdateCheckResult CheckForUpdate()
        {
            var res = new UpdateCheckResult
            {
                LatestVersion = AppMeta.Version,
                DownloadUrl = AppMeta.FallbackSetupUrl
            };

            try
            {
                string tag = null;
                string assetUrl = null;

                // 1) Try GitHub Releases API first (preferred for official releases)
                try
                {
                    string json = HttpGetJson(AppMeta.ReleasesApiUrl, 8000);
                    tag = ExtractJsonString(json, "tag_name");
                    assetUrl = ExtractSetupAssetUrl(json);
                }
                catch (WebException wex)
                {
                    var http = wex.Response as HttpWebResponse;
                    // 404 = repo has tags but no published Release yet -> fall back to Tags API
                    if (http == null || http.StatusCode != HttpStatusCode.NotFound)
                        throw;
                }

                // 2) Fallback: check GitHub Tags API if no Release published yet
                if (string.IsNullOrEmpty(tag))
                {
                    string tagsJson = HttpGetJson(AppMeta.TagsApiUrl, 8000);
                    tag = ExtractFirstTagName(tagsJson);
                }

                if (string.IsNullOrEmpty(tag))
                {
                    res.Success = true;
                    res.HasUpdate = false;
                    return res;
                }

                string cleanLatest = CleanVersion(tag);
                res.LatestVersion = cleanLatest;
                res.DownloadUrl = !string.IsNullOrEmpty(assetUrl)
                    ? assetUrl
                    : ("https://github.com/" + AppMeta.GitHubOwner + "/" + AppMeta.GitHubRepo + "/raw/" + tag + "/TouchpadGoodSetup.exe");
                res.HasUpdate = IsNewerVersion(cleanLatest, AppMeta.Version);
                res.Success = true;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Error = ex.Message;
            }

            return res;
        }

        public static string DownloadInstaller(string url, Action<int> onProgressPercent)
        {
            EnsureTls12();
            string tmpDir = Path.GetTempPath();
            string destPath = Path.Combine(tmpDir, "TouchpadGoodSetup_Update.exe");
            string partPath = destPath + ".part";

            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }

            string targetUrl = string.IsNullOrEmpty(url) ? AppMeta.FallbackSetupUrl : url;
            try
            {
                DownloadToFile(targetUrl, partPath, onProgressPercent);
            }
            catch
            {
                // Fallback to main branch installer URL if tag-specific raw URL failed
                if (!string.Equals(targetUrl, AppMeta.FallbackSetupUrl, StringComparison.OrdinalIgnoreCase))
                    DownloadToFile(AppMeta.FallbackSetupUrl, partPath, onProgressPercent);
                else
                    throw;
            }

            // Basic binary integrity verification: non-empty PE executable ("MZ" header, >= 50 KB)
            var fi = new FileInfo(partPath);
            if (!fi.Exists || fi.Length < 50000)
                throw new InvalidDataException("Downloaded update file is incomplete or invalid.");

            using (var fs = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int b0 = fs.ReadByte();
                int b1 = fs.ReadByte();
                if (b0 != 0x4D || b1 != 0x5A) // 'M', 'Z'
                    throw new InvalidDataException("Downloaded file is not a valid Windows executable.");
            }

            try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
            File.Move(partPath, destPath);
            return destPath;
        }

        private static void DownloadToFile(string url, string destPath, Action<int> onProgressPercent)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 20000;
            req.AllowAutoRedirect = true;
            req.UserAgent = AppMeta.AppName + "/" + AppMeta.Version;

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long total = resp.ContentLength;
                long received = 0;
                int lastPct = -1;
                byte[] buf = new byte[16384];
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    received += n;
                    if (onProgressPercent != null && total > 0)
                    {
                        int pct = (int)Math.Min(100, (received * 100) / total);
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            onProgressPercent(pct);
                        }
                    }
                }
            }
        }

        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ExtractFirstTagName(string tagsJson)
        {
            if (string.IsNullOrEmpty(tagsJson)) return null;
            var matches = Regex.Matches(tagsJson, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            string bestTag = null;
            Version bestVer = null;
            for (int i = 0; i < matches.Count; i++)
            {
                string t = matches[i].Groups[1].Value;
                Version v = ParseVersion(CleanVersion(t));
                if (v != null && (bestVer == null || v > bestVer))
                {
                    bestVer = v;
                    bestTag = t;
                }
            }
            return bestTag;
        }

        private static string ExtractSetupAssetUrl(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var matches = Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"");
            for (int i = 0; i < matches.Count; i++)
            {
                string u = matches[i].Groups[1].Value.Replace("\\/", "/");
                if (u.EndsWith("TouchpadGoodSetup.exe", StringComparison.OrdinalIgnoreCase))
                    return u;
            }
            return null;
        }

        private static string CleanVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "0.0.0";
            string s = tag.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(1);
            int dash = s.IndexOf('-');
            if (dash > 0) s = s.Substring(0, dash);
            return s;
        }

        private static Version ParseVersion(string v)
        {
            try
            {
                string[] parts = v.Split('.');
                int maj = parts.Length > 0 ? int.Parse(parts[0]) : 0;
                int min = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                int pat = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                int rev = parts.Length > 3 ? int.Parse(parts[3]) : 0;
                return new Version(maj, min, pat, rev);
            }
            catch { return null; }
        }

        private static bool IsNewerVersion(string latestStr, string currentStr)
        {
            Version latest = ParseVersion(latestStr);
            Version current = ParseVersion(currentStr);
            if (latest == null || current == null) return false;
            return latest > current;
        }
    }
}
