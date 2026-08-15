using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    // Auto-close on game exit: when enabled, the overlay watches the companion
    // service's GSI feed. Once a CS2/CS:GO session has been seen (GSI posts
    // flowing) and then goes silent for a sustained period, the game has exited
    // and the widget unpins itself and closes, which also shuts down the service.
    public sealed partial class KillConfirmWidgetPage
    {
        private const int AutoCloseGameExitPollMs = 10000;
        // Matches RecentGsiAgeMs used to light the live GSI indicator.
        private const double AutoCloseGameExitActiveAgeMs = 120000;
        // Consecutive silent polls (~20s) before treating the game as exited,
        // so a brief GSI hiccup or a menu heartbeat gap cannot close the widget.
        private const int AutoCloseGameExitSustainedChecks = 2;
        private static readonly Uri AutoCloseGsiStatusUri =
            new Uri("http://127.0.0.1:10087/gsi-status");

        private DispatcherTimer _autoCloseGameExitTimer;
        private bool _autoCloseGameExitActiveDetected;
        private int _autoCloseGameExitSilentChecks;
        private bool _autoCloseGameExitTriggered;

        private void StartAutoCloseGameExitMonitoring()
        {
            if (_autoCloseGameExitTimer != null || _autoCloseGameExitTriggered)
            {
                return;
            }

            _autoCloseGameExitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AutoCloseGameExitPollMs)
            };
            _autoCloseGameExitTimer.Tick += OnAutoCloseGameExitTimerTick;
            _autoCloseGameExitTimer.Start();
        }

        private void StopAutoCloseGameExitMonitoring()
        {
            if (_autoCloseGameExitTimer == null)
            {
                return;
            }

            _autoCloseGameExitTimer.Stop();
            _autoCloseGameExitTimer.Tick -= OnAutoCloseGameExitTimerTick;
            _autoCloseGameExitTimer = null;
        }

        private async void OnAutoCloseGameExitTimerTick(object sender, object e)
        {
            if (!_isPageActive || _autoCloseGameExitTriggered)
            {
                return;
            }

            if (!AutoCloseOnGameExitSettingsStore.Load())
            {
                return;
            }

            await CheckAutoCloseOnGameExitAsync();
        }

        private async Task CheckAutoCloseOnGameExitAsync()
        {
            try
            {
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (HttpResponseMessage response = await client.GetAsync(AutoCloseGsiStatusUri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        // The service is unreachable; do not make a decision on it.
                        return;
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    double posts = json.GetNamedNumber("posts", 0);
                    double? ageMs = TryGetJsonNumber(json, "last_post_age_ms");
                    bool gameActive =
                        posts > 0
                        && ageMs.HasValue
                        && ageMs.Value <= AutoCloseGameExitActiveAgeMs;

                    if (gameActive)
                    {
                        _autoCloseGameExitActiveDetected = true;
                        _autoCloseGameExitSilentChecks = 0;
                        return;
                    }

                    // The game has stopped sending GSI. Only act if it was running
                    // during this session; otherwise the user simply has not opened
                    // the game yet and we keep waiting.
                    if (!_autoCloseGameExitActiveDetected)
                    {
                        return;
                    }

                    _autoCloseGameExitSilentChecks++;
                    if (_autoCloseGameExitSilentChecks >= AutoCloseGameExitSustainedChecks)
                    {
                        await TriggerAutoCloseOnGameExitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Auto-close on game exit check failed: " + ex);
            }
        }

        private async Task TriggerAutoCloseOnGameExitAsync()
        {
            if (_autoCloseGameExitTriggered)
            {
                return;
            }

            _autoCloseGameExitTriggered = true;
            App.Log("Auto-close on game exit triggered.");
            StopAutoCloseGameExitMonitoring();

            // Best-effort unpin: the SDK exposes Pinned as read-only, so disable
            // pinning support to remove the widget from the pinned overlay.
            if (_widget != null)
            {
                try
                {
                    _widget.PinningSupported = false;
                }
                catch (Exception ex)
                {
                    App.Log("Auto-close unpin failed: " + ex);
                }
            }

            // Closing the widget window also shuts down the full-trust companion
            // service via App.OnWidgetWindowClosed -> ShutdownCompanionFromCurrentFrame.
            try
            {
                if (_widget != null)
                {
                    _widget.Close();
                }
            }
            catch (Exception ex)
            {
                App.Log("Auto-close widget Close() failed: " + ex);
                await RequestServiceShutdownAsync();
            }
        }
    }
}