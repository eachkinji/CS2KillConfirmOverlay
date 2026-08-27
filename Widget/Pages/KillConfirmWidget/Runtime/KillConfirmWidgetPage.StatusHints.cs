using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void AdvanceStatusHint()
        {
            IReadOnlyList<StatusHint> hints = BuildStatusHints();
            if (hints.Count == 0)
            {
                return;
            }

            _statusHintIndex = (_statusHintIndex + 1) % hints.Count;
            ApplyStatusHint(hints[_statusHintIndex], _statusHintIndex, hints.Count);
        }

        private void RefreshStatusHint(bool resetCycle)
        {
            IReadOnlyList<StatusHint> hints = BuildStatusHints();
            if (hints.Count == 0)
            {
                return;
            }

            if (resetCycle)
            {
                _statusHintIndex = 0;
            }
            else if (_statusHintIndex >= hints.Count)
            {
                _statusHintIndex = 0;
            }

            ApplyStatusHint(hints[_statusHintIndex], _statusHintIndex, hints.Count);
        }

        private IReadOnlyList<StatusHint> BuildStatusHints()
        {
            var hints = new List<StatusHint>();

            hints.Add(new StatusHint(LocalizationManager.Text("StatusAllLightsRequiredHint"), Color.FromArgb(255, 5, 122, 85)));
            hints.Add(new StatusHint(LocalizationManager.Text("UpdateButtonStatusHint"), Color.FromArgb(255, 180, 90, 0)));

            hints.Add(new StatusHint(LocalizationManager.Text("DisableFullscreenOptimizationsHint"), Color.FromArgb(255, 180, 90, 0)));
            hints.Add(new StatusHint(LocalizationManager.Text("CustomIconSettingsHint"), Color.FromArgb(255, 180, 90, 0)));
            hints.Add(new StatusHint(LocalizationManager.Text("ProxyPortHint"), Color.FromArgb(255, 180, 90, 0)));

            bool serviceReady = _serviceConnectionState == KillEventConnectionState.Connected;
            bool cfgReady = _cfgDetectionState == CfgDetectionState.Ready;
            bool animationReady = _animationCacheReady;

            if (serviceReady && cfgReady && _gsiRecentlySeen && animationReady)
            {
                hints.Add(new StatusHint(LocalizationManager.Text("ReadyAllSignals"), Color.FromArgb(255, 5, 122, 85)));
            }

            hints.Add(new StatusHint(GetServiceStatusHint(), GetServiceHintColor()));
            hints.Add(new StatusHint(GetCfgStatusHint(), GetCfgHintColor()));
            hints.Add(new StatusHint(GetGsiStatusHint(), GetGsiHintColor()));
            hints.Add(new StatusHint(GetAnimationStatusHint(), GetAnimationHintColor()));

            return hints;
        }

        private void ApplyStatusHint(StatusHint hint, int index, int total)
        {
            ShowStatusHint(hint.Text, hint.Color, index, total);
        }

        private void ShowStatusHint(string text, Color color, int index = 0, int total = 1)
        {
            bool changed = !string.Equals(_currentStatusHintText, text, StringComparison.Ordinal);
            _currentStatusHintText = text;
            HeaderStatusSection.PinHintText.Text = text;
            HeaderStatusSection.PinHintText.Foreground = new SolidColorBrush(color);
            HeaderStatusSection.StatusHintProgressFill.Background = new SolidColorBrush(color);
            HeaderStatusSection.StatusHintPagerText.Text = total > 0 ? $"{index + 1}/{total}" : string.Empty;
            UpdateStatusHintProgress(index, total);
            if (changed)
            {
                AnimateStatusHintChange();
            }

            ToolTipService.SetToolTip(HeaderStatusSection.StatusHintBox, text);
        }

        // Show a one-off hint immediately. The 3-second status-hint rotation
        // overwrites it on the next tick, so this reads as a brief flash.
        private void ShowTransientStatusHint(string text, Color color)
        {
            if (HeaderStatusSection.PinHintText == null)
            {
                return;
            }

            ShowStatusHint(text, color, 0, 1);
        }

        private void UpdateStatusHintProgress(int index, int total)
        {
            if (HeaderStatusSection.StatusHintProgressScale == null)
            {
                return;
            }

            double progress = total > 0
                ? Math.Max(0.0, Math.Min(1.0, (index + 1.0) / total))
                : 0.0;
            HeaderStatusSection.StatusHintProgressScale.ScaleX = progress;
        }

        private void AnimateStatusHintChange()
        {
            if (HeaderStatusSection.PinHintText == null)
            {
                return;
            }

            var storyboard = new Storyboard();
            var fade = new DoubleAnimation
            {
                From = 0.15,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EnableDependentAnimation = true
            };

            Storyboard.SetTarget(fade, HeaderStatusSection.PinHintText);
            Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);
            storyboard.Begin();
        }

        private static void SetNamedToolTip(DependencyObject target, string title, string description)
        {
            if (target == null)
            {
                return;
            }

            ToolTipService.SetToolTip(target, BuildToolTipText(title, description));
        }

        private static string BuildToolTipText(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return description ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return title;
            }

            return string.Equals(title, description, StringComparison.Ordinal)
                ? title
                : title + "\n" + description;
        }

        private string GetServiceStatusHint()
        {
            switch (_serviceConnectionState)
            {
                case KillEventConnectionState.Connected:
                    return LocalizationManager.Text("StatusSvcReady");
                case KillEventConnectionState.Connecting:
                    return LocalizationManager.Text("StatusSvcStarting");
                default:
                    return _currentServiceDiagnostic == null
                        ? LocalizationManager.Text("StatusSvcOffline")
                        : FormatServiceDiagnostic(_currentServiceDiagnostic);
            }
        }

        private Color GetServiceHintColor()
        {
            switch (_serviceConnectionState)
            {
                case KillEventConnectionState.Connected:
                    return Color.FromArgb(255, 5, 122, 85);
                case KillEventConnectionState.Connecting:
                    return Color.FromArgb(255, 180, 90, 0);
                default:
                    return Color.FromArgb(255, 185, 28, 28);
            }
        }

        private string GetCfgStatusHint()
        {
            switch (_cfgDetectionState)
            {
                case CfgDetectionState.Ready:
                    return LocalizationManager.Text("StatusCfgReady");
                case CfgDetectionState.Checking:
                    return LocalizationManager.Text("StatusCfgChecking");
                case CfgDetectionState.Missing:
                    return LocalizationManager.Text("StatusCfgMissing");
                case CfgDetectionState.Outdated:
                    return LocalizationManager.Text("StatusCfgOutdated");
                case CfgDetectionState.Error:
                    return LocalizationManager.Text("StatusCfgError");
                default:
                    return LocalizationManager.Text("StatusCfgSelect");
            }
        }

        private Color GetCfgHintColor()
        {
            switch (_cfgDetectionState)
            {
                case CfgDetectionState.Ready:
                    return Color.FromArgb(255, 5, 122, 85);
                case CfgDetectionState.Outdated:
                case CfgDetectionState.Error:
                    return Color.FromArgb(255, 185, 28, 28);
                default:
                    return Color.FromArgb(255, 180, 90, 0);
            }
        }

        private string GetGsiStatusHint()
        {
            if (_gsiRecentlySeen)
            {
                return LocalizationManager.Text("StatusGsiReady");
            }

            if (_serviceConnectionState != KillEventConnectionState.Connected)
            {
                return LocalizationManager.Text("StatusGsiNeedsService");
            }

            return LocalizationManager.Text("StatusGsiWaiting");
        }

        private Color GetGsiHintColor()
        {
            if (_gsiRecentlySeen)
            {
                return Color.FromArgb(255, 5, 122, 85);
            }

            return _serviceConnectionState == KillEventConnectionState.Connected
                ? Color.FromArgb(255, 180, 90, 0)
                : Color.FromArgb(255, 75, 85, 99);
        }

        private string GetAnimationStatusHint()
        {
            if (_animationCacheReady)
            {
                return LocalizationManager.Text("StatusAniReady");
            }

            if (_animationCacheFailed)
            {
                return LocalizationManager.Text("StatusAniFailed");
            }

            return LocalizationManager.Text("StatusAniLoading") + Math.Max(0, Math.Min(99, _animationCacheProgress)) + "%";
        }

        private Color GetAnimationHintColor()
        {
            if (_animationCacheReady)
            {
                return Color.FromArgb(255, 5, 122, 85);
            }

            if (_animationCacheFailed)
            {
                return Color.FromArgb(255, 185, 28, 28);
            }

            return Color.FromArgb(255, 180, 90, 0);
        }

        private sealed class StatusHint
        {
            public StatusHint(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }

            public Color Color { get; }
        }
    }
}
