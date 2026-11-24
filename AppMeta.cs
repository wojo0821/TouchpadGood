using System;
using System.IO;
using System.Reflection;

[assembly: AssemblyTitle("TouchpadGood")]
[assembly: AssemblyDescription("Ultra-low-latency gaming & productivity Precision Touchpad driver for Windows 11")]
[assembly: AssemblyProduct("TouchpadGood")]
[assembly: AssemblyCompany("TouchpadGood")]
[assembly: AssemblyCopyright("Copyright (c) 2026 wojo0821")]
[assembly: AssemblyVersion("1.0.4.0")]
[assembly: AssemblyFileVersion("1.0.4.0")]

namespace TouchpadGood
{
    internal static class AppMeta
    {
        public const string AppName = "TouchpadGood";
        public const string Version = "1.0.4";
        public const string GitHubOwner = "wojo0821";
        public const string GitHubRepo = "TouchpadGood";
        public const string ReleasesApiUrl = "https://api.github.com/repos/" + GitHubOwner + "/" + GitHubRepo + "/releases/latest";
        public const string TagsApiUrl = "https://api.github.com/repos/" + GitHubOwner + "/" + GitHubRepo + "/tags";
        public const string FallbackSetupUrl = "https://github.com/" + GitHubOwner + "/" + GitHubRepo + "/raw/main/TouchpadGoodSetup.exe";
        public const string RepoWebUrl = "https://github.com/" + GitHubOwner + "/" + GitHubRepo;

        public static void LogCrash(string component, Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppName);
                Directory.CreateDirectory(dir);
                string logPath = Path.Combine(dir, "crash.log");
                string entry = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] [" + component + " v" + Version + "]\r\n"
                    + (ex != null ? ex.ToString() : "Unknown error") + "\r\n----------------------------------------\r\n";
                File.AppendAllText(logPath, entry);
            }
            catch { }
        }
    }
}
