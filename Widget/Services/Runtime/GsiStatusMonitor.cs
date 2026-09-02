using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    public sealed class GsiStatusSnapshot
    {
        public bool ServiceReachable { get; }
        public bool IsGreen { get; }
        public double Posts { get; }
        public double? LastPostAgeMs { get; }
        public double ParseErrors { get; }

        public GsiStatusSnapshot(
            bool serviceReachable,
            bool isGreen,
            double posts,
            double? lastPostAgeMs,
            double parseErrors)
        {
            ServiceReachable = serviceReachable;
            IsGreen = isGreen;
            Posts = posts;
            LastPostAgeMs = lastPostAgeMs;
            ParseErrors = parseErrors;
        }

        public static GsiStatusSnapshot Offline { get; } =
            new GsiStatusSnapshot(false, false, 0, null, 0);
    }

    public sealed class GsiStatusMonitor : IDisposable
    {
        public const double RecentGsiAgeMs = 120000.0;
        public const int DefaultPollIntervalMs = 5000;

        private static readonly Lazy<GsiStatusMonitor> LazyInstance =
            new Lazy<GsiStatusMonitor>(() => new GsiStatusMonitor());

        public static GsiStatusMonitor Instance => LazyInstance.Value;

        private readonly object _syncRoot = new object();
        private GsiStatusSnapshot _currentSnapshot = GsiStatusSnapshot.Offline;
        private Task<GsiStatusSnapshot> _currentRefreshTask;
        private CancellationTokenSource _pollCancellation;
        private int _subscriberCount;
        private bool _disposed;

        public event Action<GsiStatusSnapshot> StatusUpdated;
        public event Action<bool> GreenStateChanged;

        public GsiStatusSnapshot CurrentSnapshot
        {
            get
            {
                lock (_syncRoot)
                {
                    return _currentSnapshot;
                }
            }
        }

        public bool IsGreen => CurrentSnapshot.IsGreen;

        public void StartMonitoring(int intervalMs = DefaultPollIntervalMs)
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _subscriberCount++;
                if (_subscriberCount == 1)
                {
                    _pollCancellation = new CancellationTokenSource();
                    _ = RunPollingLoopAsync(intervalMs, _pollCancellation.Token);
                }
            }
        }

        public void StopMonitoring()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _subscriberCount = Math.Max(0, _subscriberCount - 1);
                if (_subscriberCount == 0)
                {
                    if (_pollCancellation != null)
                    {
                        try
                        {
                            _pollCancellation.Cancel();
                            _pollCancellation.Dispose();
                        }
                        catch
                        {
                        }
                        _pollCancellation = null;
                    }

                    // Invalidate snapshot when no consumers are active to prevent stale green reopen
                    _currentSnapshot = GsiStatusSnapshot.Offline;
                }
            }
        }

        public Task<GsiStatusSnapshot> RefreshAsync()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return Task.FromResult(GsiStatusSnapshot.Offline);
                }

                // If a refresh is already in-flight, join it to prevent concurrent redundant HTTP requests
                // and avoid returning stale snapshots or triggering false edges.
                if (_currentRefreshTask != null && !_currentRefreshTask.IsCompleted)
                {
                    return _currentRefreshTask;
                }

                _currentRefreshTask = PerformRefreshAsync();
                return _currentRefreshTask;
            }
        }

        private async Task<GsiStatusSnapshot> PerformRefreshAsync()
        {
            GsiStatusSnapshot newSnapshot;
            try
            {
                var uri = LocalServiceEndpoints.Build("/gsi-status");
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var response = await client.GetAsync(uri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        newSnapshot = GsiStatusSnapshot.Offline;
                    }
                    else
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        JsonObject json = JsonObject.Parse(responseText);
                        double posts = json.GetNamedNumber("posts", 0);
                        double parseErrors = json.GetNamedNumber("parse_errors", 0);
                        double? ageMs = TryGetJsonNumber(json, "last_post_age_ms");
                        bool recentlySeen = posts > 0 && ageMs.HasValue && ageMs.Value <= RecentGsiAgeMs;
                        newSnapshot = new GsiStatusSnapshot(true, recentlySeen, posts, ageMs, parseErrors);
                    }
                }
            }
            catch
            {
                newSnapshot = GsiStatusSnapshot.Offline;
            }

            UpdateSnapshot(newSnapshot);
            return newSnapshot;
        }

        private void UpdateSnapshot(GsiStatusSnapshot newSnapshot)
        {
            GsiStatusSnapshot previousSnapshot;
            bool greenChanged = false;

            lock (_syncRoot)
            {
                previousSnapshot = _currentSnapshot;
                _currentSnapshot = newSnapshot;
                if (previousSnapshot.IsGreen != newSnapshot.IsGreen)
                {
                    greenChanged = true;
                }
            }

            StatusUpdated?.Invoke(newSnapshot);
            if (greenChanged)
            {
                GreenStateChanged?.Invoke(newSnapshot.IsGreen);
            }
        }

        private async Task RunPollingLoopAsync(int intervalMs, CancellationToken token)
        {
            while (!token.IsCancellationRequested && !_disposed)
            {
                try
                {
                    await RefreshAsync();
                    await Task.Delay(intervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    App.Log("GsiStatusMonitor polling error: " + ex.Message);
                    try
                    {
                        await Task.Delay(intervalMs, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private static double? TryGetJsonNumber(JsonObject json, string name)
        {
            if (json != null && json.ContainsKey(name))
            {
                var value = json.GetNamedValue(name);
                if (value.ValueType == JsonValueType.Number)
                {
                    return value.GetNumber();
                }
            }
            return null;
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                try
                {
                    _pollCancellation?.Cancel();
                    _pollCancellation?.Dispose();
                }
                catch
                {
                }
                _pollCancellation = null;
                _subscriberCount = 0;
            }
        }
    }
}
