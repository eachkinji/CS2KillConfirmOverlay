using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    public sealed class KillEventClient : IDisposable
    {
        private const int PollWaitMilliseconds = 8000;
        private const int PollTimeoutMilliseconds = 12000;
        private const int ReconnectDelayMilliseconds = 1000;
        private static readonly Uri EventsBaseUri = LocalServiceEndpoints.Build("/events");

        private readonly CoreDispatcher _dispatcher;
        private HttpClient _httpClient;
        private CancellationTokenSource _runCancellation;
        private ulong _cursor;
        private bool _skipBacklog = true;
        private DateTimeOffset _lastDroppedNotice = DateTimeOffset.MinValue;
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
        public event EventHandler<EventsDroppedEventArgs> EventsDropped;

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

            ulong dropped = ToUInt64(batch.GetNamedNumber("dropped", 0));
            if (dropped > 0)
            {
                ReportDroppedEvents(dropped);
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
                if (killEvent.PublishedUnixMs > 0)
                {
                    ulong nowMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    ulong latencyMs = nowMs > killEvent.PublishedUnixMs
                        ? nowMs - killEvent.PublishedUnixMs
                        : 0;
                    App.Log("[perf] publish_to_widget_ms=" + latencyMs
                        + ", event_id=" + eventId
                        + ", channel=" + killEvent.EventChannel);
                }
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

        private void ReportDroppedEvents(ulong dropped)
        {
            App.Log(
                "HTTP event poll dropped " + dropped + " event(s): queue overflowed while the widget was not polling.");
            if (DateTimeOffset.Now - _lastDroppedNotice > TimeSpan.FromSeconds(15))
            {
                _lastDroppedNotice = DateTimeOffset.Now;
                EventsDropped?.Invoke(this, new EventsDroppedEventArgs(dropped));
            }
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
                IsGrenadeKill = json.GetNamedBoolean("is_grenade_kill", false),
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
                SteamId = json.GetNamedString("steamid", string.Empty),
                PublishedUnixMs = ToUInt64(json.GetNamedNumber("published_unix_ms", 0))
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
