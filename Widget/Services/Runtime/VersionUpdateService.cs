using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Web.Http;
namespace KillConfirmGameBar.Services
{
    internal enum VersionUpdateState { NotChecked, Checking, UpdateAvailable, UpToDate, Ahead, Unavailable }
    internal static class VersionUpdateService
    {
        internal const string ReleasesUrl = "https://github.com/eachkinji/CS2KillConfirmOverlay/releases";
        internal const string DownloadUrl = "https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv";
        internal const string DownloadCode = "7Twv";
        internal static VersionUpdateState State { get; private set; }
        internal static string LatestVersion { get; private set; } = "";
        internal static string ReleaseNotes { get; private set; } = "";
        internal static string ReleaseUrl { get; private set; } = ReleasesUrl;
        internal static DateTimeOffset? CheckedAt { get; private set; }
        internal static DateTimeOffset? PublishedAt { get; private set; }
        internal static event EventHandler Changed;
        private static Task _checking;
        internal static Func<Task<string>> FetchOverride;
        internal static Version CurrentVersion
        {
            get { var v = Package.Current.Id.Version; return new Version(v.Major, v.Minor, v.Build, v.Revision); }
        }
        internal static Task CheckAsync(bool force = false)
        {
            if (_checking != null && !_checking.IsCompleted) return _checking;
            if (!force && CheckedAt.HasValue && DateTimeOffset.UtcNow - CheckedAt.Value < TimeSpan.FromMinutes(State == VersionUpdateState.Unavailable ? 5 : 360)) return Task.CompletedTask;
            return _checking = CheckCoreAsync();
        }
        private static async Task CheckCoreAsync()
        {
            State = VersionUpdateState.Checking; Changed?.Invoke(null, EventArgs.Empty);
            try
            {
                string json;
                if (FetchOverride != null) json = await FetchOverride();
                else
                {
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.TryAppendWithoutValidation("User-Agent", "KillConfirmOverlay/" + CurrentVersion);
                        json = await client.GetStringAsync(new Uri("https://api.github.com/repos/eachkinji/CS2KillConfirmOverlay/releases/latest")).AsTask(timeout.Token);
                    }
                }
                ApplyRelease(json, CurrentVersion);
            }
            catch (Exception error)
            {
                State = VersionUpdateState.Unavailable;
                App.Log("Version check: " + error.Message);
            }
            finally { CheckedAt = DateTimeOffset.UtcNow; Changed?.Invoke(null, EventArgs.Empty); }
        }
        internal static void ApplyRelease(string payload, Version current)
        {
            var release = JsonObject.Parse(payload);
            string tag = release.GetNamedString("tag_name", "").Trim().TrimStart('v', 'V');
            if (release.GetNamedBoolean("draft", false) || release.GetNamedBoolean("prerelease", false) || !Version.TryParse(tag, out Version parsed))
                throw new FormatException("No valid stable release version.");
            var latest = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
            LatestVersion = latest.ToString();
            ReleaseNotes = release.GetNamedString("body", "");
            string link = release.GetNamedString("html_url", "");
            ReleaseUrl = link.StartsWith(ReleasesUrl + "/", StringComparison.OrdinalIgnoreCase) ? link : ReleasesUrl;
            PublishedAt = DateTimeOffset.TryParse(release.GetNamedString("published_at", ""), out DateTimeOffset published) ? published : (DateTimeOffset?)null;
            State = current < latest ? VersionUpdateState.UpdateAvailable : current > latest ? VersionUpdateState.Ahead : VersionUpdateState.UpToDate;
        }
        internal static string StatusText(bool zh)
        {
            switch (State)
            {
                case VersionUpdateState.Checking: return zh ? "正在检查更新…" : "Checking for updates…";
                case VersionUpdateState.UpdateAvailable: return (zh ? "有新版本 " : "Update available: ") + LatestVersion;
                case VersionUpdateState.UpToDate: return zh ? "已是最新版本" : "Up to date";
                case VersionUpdateState.Ahead: return zh ? "当前版本较新" : "Ahead of public release";
                case VersionUpdateState.Unavailable: return zh ? "暂时无法检查更新" : "Update check unavailable";
                default: return zh ? "检查更新 · 关于本版本" : "Updates · About this build";
            }
        }
    }
}
