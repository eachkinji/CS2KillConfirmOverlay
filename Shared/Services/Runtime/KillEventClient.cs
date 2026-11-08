// Shared by Widget (UWP) and CompatibilityHost; see Shared/Events/KillEventContract.cs.
// `App` resolves to each host's own App class through the enclosing namespace.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.Web.Http;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    public sealed class KillEventClient : IDisposable
    {
        private const int PollWaitMilliseconds = 8000;
        private const int PollTimeoutMilliseconds = 12000;
        private const int ReconnectDelayMilliseconds = 1000;
        // Evaluated per poll: the desktop host can change its local service port.
        private static Uri EventsBaseUri => LocalServiceEndpoints.Build("/events");

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
        /// <summary>Observation records (e.g. every LOL Riot event); never routed to styles.</summary>
        public event EventHandler<KillEvent> ObservationReceived;
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
            var parsed = new List<KeyValuePair<ulong, KillEvent>>();
            bool generationChanged = false;
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

                KillEvent killEvent = KillEventParser.Parse(json);
                generationChanged |= killEvent.SourceGeneration != GameSourceSettingsStore.Generation;
                parsed.Add(new KeyValuePair<ulong, KillEvent>(eventId, killEvent));
            }

            // Events carry their source and generation. Only a generation this UI
            // has not seen (switch from the other UI, service restart) needs the
            // authoritative selection; steady-state batches skip that round trip.
            if (generationChanged) await GameSourceSettingsStore.RefreshAsync();

            int delivered = 0;
            foreach (KeyValuePair<ulong, KillEvent> item in parsed)
            {
                ulong eventId = item.Key;
                KillEvent killEvent = item.Value;
                if (killEvent.SourceGeneration != GameSourceSettingsStore.Generation) { _cursor = eventId; continue; }
                if (killEvent.PublishedUnixMs > 0 && !killEvent.IsObservationEvent)
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

        private async Task DispatchKillEventAsync(KillEvent killEvent)
        {
            EventHandler<KillEvent> handler = killEvent.IsObservationEvent ? ObservationReceived : KillReceived;
            if (_dispatcher == null)
            {
                if (killEvent.SourceGeneration == GameSourceSettingsStore.Generation)
                {
                    handler?.Invoke(this, killEvent);
                    if (killEvent.IsObservationEvent
                        && LolEventAdapter.TryPromoteObservation(killEvent, GameStyleService.Current, out KillEvent promoted))
                    {
                        KillReceived?.Invoke(this, promoted);
                    }
                }
                return;
            }

            CoreDispatcherPriority priority = killEvent.IsObservationEvent
                ? CoreDispatcherPriority.Low
                : CoreDispatcherPriority.High;
            await _dispatcher.RunAsync(priority, () =>
            {
                EventHandler<KillEvent> current = killEvent.IsObservationEvent ? ObservationReceived : KillReceived;
                if (killEvent.SourceGeneration == GameSourceSettingsStore.Generation)
                {
                    current?.Invoke(this, killEvent);
                    if (killEvent.IsObservationEvent
                        && LolEventAdapter.TryPromoteObservation(killEvent, GameStyleService.Current, out KillEvent promoted))
                    {
                        KillReceived?.Invoke(this, promoted);
                    }
                }
            });
        }

        private static ulong ToUInt64(double value)
        {
            return KillEventParser.ToUInt64(value);
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
