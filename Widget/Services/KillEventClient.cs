using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    public enum KillEventConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    public enum ServiceConnectionFailureKind
    {
        ConnectFailed,
        ConnectionClosed,
        AuthenticationFailed,
        MessageReadFailed
    }

    public sealed class ServiceConnectionFailureEventArgs : EventArgs
    {
        public ServiceConnectionFailureKind Kind { get; set; }
        public int HResult { get; set; }
        public string Detail { get; set; }
    }

    public sealed class KillEvent
    {
        public string EventChannel { get; set; }
        public int KillCount { get; set; }
        public bool IsHeadshot { get; set; }
        public bool IsKnifeKill { get; set; }
        public bool IsFirstKill { get; set; }
        public bool IsLastKill { get; set; }
        public bool IsAssist { get; set; }
        public bool PlayMainAnimation { get; set; }
        public string AnimationKey { get; set; }
        public string EventKind { get; set; }
        public string WeaponBadgeKey { get; set; }
        public string WeaponName { get; set; }
        public int MoneyReward { get; set; }
        public int RoundNumber { get; set; }
        public int MoneyEpoch { get; set; }
        public string PlayerName { get; set; }
        public string TargetName { get; set; }
        public string SteamId { get; set; }

        public bool IsCombatEvent
        {
            get { return string.Equals(EventChannel, KillEventChannels.Combat, StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsEconomyEvent
        {
            get { return string.Equals(EventChannel, KillEventChannels.Economy, StringComparison.OrdinalIgnoreCase); }
        }
    }

    public static class KillEventChannels
    {
        public const string Combat = "combat";
        public const string Economy = "economy";

        public static string Normalize(string eventChannel, string eventKind, bool isAssist)
        {
            if (string.Equals(eventChannel, Combat, StringComparison.OrdinalIgnoreCase))
            {
                return Combat;
            }

            if (string.Equals(eventChannel, Economy, StringComparison.OrdinalIgnoreCase))
            {
                return Economy;
            }

            return IsEconomyKind(eventKind) && !isAssist ? Economy : Combat;
        }

        private static bool IsEconomyKind(string eventKind)
        {
            return string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class KillEventClient : IDisposable
    {
        private const int PollWaitMilliseconds = 8000;
        private const int PollTimeoutMilliseconds = 12000;
        private const int ReconnectDelayMilliseconds = 1000;
        private static readonly Uri EventsBaseUri = new Uri("http://127.0.0.1:10087/events");

        private readonly CoreDispatcher _dispatcher;
        private HttpClient _httpClient;
        private CancellationTokenSource _runCancellation;
        private ulong _cursor;
        private bool _skipBacklog = true;
        private long _pollSequence;
        private bool _started;
        private bool _disposed;
        private KillEventConnectionState _connectionState = KillEventConnectionState.Disconnected;

        public KillEventClient(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public event EventHandler<KillEvent> KillReceived;
        public event EventHandler<KillEventConnectionState> ConnectionStateChanged;
        public event EventHandler<ServiceConnectionFailureEventArgs> ConnectionFailure;

        public KillEventConnectionState ConnectionState => _connectionState;

        public void Start()
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _runCancellation = new CancellationTokenSource();
            _ = RunAsync(_runCancellation.Token);
        }

        public void Dispose()
        {
            _disposed = true;
            _started = false;
            try
            {
                _runCancellation?.Cancel();
            }
            catch
            {
            }

            CleanupHttpClient();
            _runCancellation?.Dispose();
            _runCancellation = null;
            SetConnectionState(KillEventConnectionState.Disconnected);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!_disposed && _started && !cancellationToken.IsCancellationRequested)
            {
                HttpClient pollClient = null;
                long pollId = Interlocked.Increment(ref _pollSequence);
                try
                {
                    if (_connectionState == KillEventConnectionState.Disconnected)
                    {
                        SetConnectionState(KillEventConnectionState.Connecting);
                    }

                    // A fresh client plus Connection: close prevents a broken HTTP/1.0
                    // keep-alive connection from hanging every poll after the first event.
                    pollClient = await LocalServiceAuth.CreateHttpClientAsync();
                    pollClient.DefaultRequestHeaders.TryAppendWithoutValidation("Connection", "close");
                    _httpClient = pollClient;

                    var requestUri = new Uri(
                        EventsBaseUri + "?after=" + _cursor
                        + "&wait_ms=" + PollWaitMilliseconds
                        + "&skip_backlog=" + (_skipBacklog ? "true" : "false"));

                    var elapsed = Stopwatch.StartNew();
                    App.Log(
                        "HTTP event poll started: id=" + pollId
                        + ", after=" + _cursor
                        + ", skipBacklog=" + _skipBacklog);

                    using (var pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    using (HttpResponseMessage response = await GetWithTimeoutAsync(
                        pollClient,
                        requestUri,
                        pollCancellation,
                        cancellationToken))
                    {
                        elapsed.Stop();
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            LocalServiceAuth.InvalidateCachedToken();
                            throw new UnauthorizedAccessException("Local service authentication was rejected.");
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);
                        }

                        SetConnectionState(KillEventConnectionState.Connected);
                        if (response.StatusCode == HttpStatusCode.NoContent)
                        {
                            _skipBacklog = false;
                            App.Log(
                                "HTTP event poll completed: id=" + pollId
                                + ", status=204, elapsedMs=" + elapsed.ElapsedMilliseconds);
                            continue;
                        }

                        string payload = await response.Content.ReadAsStringAsync();
                        int delivered = await ProcessBatchAsync(payload);
                        _skipBacklog = false;
                        App.Log(
                            "HTTP event poll completed: id=" + pollId
                            + ", status=" + (int)response.StatusCode
                            + ", delivered=" + delivered
                            + ", cursor=" + _cursor
                            + ", elapsedMs=" + elapsed.ElapsedMilliseconds);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    bool wasConnected = _connectionState == KillEventConnectionState.Connected;
                    ReportConnectionFailure(new ServiceConnectionFailureEventArgs
                    {
                        Kind = ex is UnauthorizedAccessException
                            ? ServiceConnectionFailureKind.AuthenticationFailed
                            : wasConnected
                                ? ServiceConnectionFailureKind.ConnectionClosed
                                : ServiceConnectionFailureKind.ConnectFailed,
                        HResult = ex.HResult,
                        Detail = ex.Message
                    });
                    App.Log(
                        "HTTP event poll failed: id=" + pollId
                        + ", cursor=" + _cursor
                        + ", hresult=0x" + ex.HResult.ToString("X8")
                        + ", detail=" + ex.Message);

                    // Only the first successful attachment may skip pre-existing events.
                    // Retain the cursor and backlog after transient failures so kills that
                    // arrived during reconnection are delivered instead of discarded.
                    SetConnectionState(KillEventConnectionState.Disconnected);

                    try
                    {
                        await Task.Delay(ReconnectDelayMilliseconds, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    ReleaseHttpClient(pollClient);
                }
            }
        }

        private static async Task<HttpResponseMessage> GetWithTimeoutAsync(
            HttpClient client,
            Uri requestUri,
            CancellationTokenSource pollCancellation,
            CancellationToken runCancellation)
        {
            pollCancellation.CancelAfter(PollTimeoutMilliseconds);
            try
            {
                return await client.GetAsync(requestUri).AsTask(pollCancellation.Token);
            }
            catch (OperationCanceledException) when (!runCancellation.IsCancellationRequested)
            {
                throw new TimeoutException(
                    "Local event poll exceeded " + PollTimeoutMilliseconds + " ms.");
            }
        }

        private async Task<int> ProcessBatchAsync(string payload)
        {
            JsonObject batch = JsonObject.Parse(payload);
            ulong batchCursor = ToUInt64(batch.GetNamedNumber("cursor", 0));
            if (batchCursor < _cursor)
            {
                _cursor = 0;
            }

            JsonArray events = batch.GetNamedArray("events", new JsonArray());
            int delivered = 0;
            foreach (IJsonValue value in events)
            {
                if (value.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                JsonObject json = value.GetObject();
                ulong eventId = ToUInt64(json.GetNamedNumber("id", 0));
                if (eventId == 0 || eventId <= _cursor)
                {
                    continue;
                }

                KillEvent killEvent = ParseKillEvent(json);
                await DispatchKillEventAsync(killEvent);
                _cursor = eventId;
                delivered++;
                App.Log(
                    "HTTP kill event dispatched: id=" + eventId
                    + ", channel=" + killEvent.EventChannel
                    + ", kind=" + killEvent.EventKind
                    + ", kills=" + killEvent.KillCount);
            }

            if (events.Count == 0 && batchCursor > 0)
            {
                _cursor = batchCursor;
            }

            return delivered;
        }

        private static KillEvent ParseKillEvent(JsonObject json)
        {
            bool isAssist = json.GetNamedBoolean("is_assist", false);
            string eventKind = json.GetNamedString("event_kind", string.Empty);
            string eventChannel = KillEventChannels.Normalize(
                json.GetNamedString("event_channel", string.Empty),
                eventKind,
                isAssist);

            return new KillEvent
            {
                EventChannel = eventChannel,
                KillCount = (int)json.GetNamedNumber("kill_count", 0),
                IsHeadshot = json.GetNamedBoolean("is_headshot", false),
                IsKnifeKill = json.GetNamedBoolean("is_knife_kill", false),
                IsFirstKill = json.GetNamedBoolean("is_first_kill", false),
                IsLastKill = json.GetNamedBoolean("is_last_kill", false),
                IsAssist = isAssist,
                PlayMainAnimation = json.GetNamedBoolean("play_main_animation", true),
                AnimationKey = json.GetNamedString("animation_key", string.Empty),
                EventKind = eventKind,
                WeaponBadgeKey = json.GetNamedString("weapon_badge_key", string.Empty),
                WeaponName = json.GetNamedString("weapon_name", string.Empty),
                MoneyReward = (int)json.GetNamedNumber("money_reward", 0),
                RoundNumber = (int)json.GetNamedNumber("round_number", 0),
                MoneyEpoch = (int)json.GetNamedNumber("money_epoch", 0),
                PlayerName = json.GetNamedString("player_name", string.Empty),
                TargetName = json.GetNamedString("target_name", string.Empty),
                SteamId = json.GetNamedString("steamid", string.Empty)
            };
        }

        private async Task DispatchKillEventAsync(KillEvent killEvent)
        {
            if (_dispatcher == null)
            {
                KillReceived?.Invoke(this, killEvent);
                return;
            }

            await _dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                KillReceived?.Invoke(this, killEvent);
            });
        }

        private static ulong ToUInt64(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return 0;
            }

            return value >= ulong.MaxValue ? ulong.MaxValue : (ulong)value;
        }

        private void CleanupHttpClient()
        {
            HttpClient client = _httpClient;
            if (client == null)
            {
                return;
            }

            _httpClient = null;
            try
            {
                client.Dispose();
            }
            catch
            {
            }
        }

        private void ReleaseHttpClient(HttpClient client)
        {
            if (client == null)
            {
                return;
            }

            if (ReferenceEquals(_httpClient, client))
            {
                _httpClient = null;
            }

            try
            {
                client.Dispose();
            }
            catch
            {
            }
        }

        private void ReportConnectionFailure(ServiceConnectionFailureEventArgs failure)
        {
            if (_disposed || failure == null)
            {
                return;
            }

            if (_dispatcher == null || _dispatcher.HasThreadAccess)
            {
                ConnectionFailure?.Invoke(this, failure);
                return;
            }

            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ConnectionFailure?.Invoke(this, failure);
            });
        }

        private void SetConnectionState(KillEventConnectionState state)
        {
            if (_connectionState == state)
            {
                return;
            }

            _connectionState = state;

            if (_dispatcher == null || _dispatcher.HasThreadAccess)
            {
                ConnectionStateChanged?.Invoke(this, state);
                return;
            }

            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ConnectionStateChanged?.Invoke(this, state);
            });
        }
    }
}
