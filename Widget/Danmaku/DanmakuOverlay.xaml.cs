using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Danmaku
{
    public sealed partial class DanmakuOverlay : UserControl
    {
        private sealed class ActiveDanmaku
        {
            public string Text;
            public float X;
            public float Y;
            public float SpeedPxPerSec;
            public float MeasuredWidth;
            public Color Color;
        }

        private sealed class PendingDanmaku
        {
            public string Text;
            public float TargetY;
            public float SpeedPxPerSec;
            public long TriggerTimeMs;
            public Color Color;
        }

        private static readonly Random _rand = new Random();

        private static readonly Color WhiteColor = Colors.White;
        private static readonly Color GoldColor = Color.FromArgb(255, 252, 211, 77);
        private static readonly Color CyanColor = Color.FromArgb(255, 103, 232, 249);
        private static readonly Color ShadowBgColor = Color.FromArgb(145, 0, 0, 0);
        private static readonly Color TextOutlineColor = Color.FromArgb(240, 15, 15, 15);

        private readonly List<ActiveDanmaku> _activeList = new List<ActiveDanmaku>();
        private readonly List<PendingDanmaku> _pendingList = new List<PendingDanmaku>();
        private readonly Stopwatch _animationStopwatch = new Stopwatch();
        private long _lastFrameMs;

        private bool _isRendering;
        private CanvasTextFormat _cachedTextFormat;
        private int _cachedFontSize = 16;
        private FontWeight _cachedFontWeight = FontWeights.SemiBold;
        private bool _cachedShowBackground;
        private bool _cachedShowOutline = true;

        public DanmakuOverlay()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = DanmakuRepository.EnsureLoadedAsync();
            DanmakuSettingsStore.TestRequested -= OnTestRequested;
            DanmakuSettingsStore.TestRequested += OnTestRequested;
            DanmakuSettingsStore.KillTestRequested -= OnKillTestRequested;
            DanmakuSettingsStore.KillTestRequested += OnKillTestRequested;
            DanmakuSettingsStore.DeathTestRequested -= OnDeathTestRequested;
            DanmakuSettingsStore.DeathTestRequested += OnDeathTestRequested;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DanmakuSettingsStore.TestRequested -= OnTestRequested;
            DanmakuSettingsStore.KillTestRequested -= OnKillTestRequested;
            DanmakuSettingsStore.DeathTestRequested -= OnDeathTestRequested;
            StopRendering();
            lock (_activeList)
            {
                _activeList.Clear();
            }
            lock (_pendingList)
            {
                _pendingList.Clear();
            }
            _cachedTextFormat?.Dispose();
            _cachedTextFormat = null;
        }

        private void OnTestRequested()
        {
            TriggerKillBarrage();
        }

        private void OnKillTestRequested()
        {
            TriggerKillBarrage();
        }

        private void OnDeathTestRequested()
        {
            TriggerDeathBarrage();
        }

        public void TriggerKillBarrage(int? customCount = null, double? customDurationSeconds = null)
        {
            int count = customCount ?? DanmakuSettingsStore.Count;
            IReadOnlyList<string> items = DanmakuRepository.GetRandomKillBatch(count);
            TriggerBarrageInternal(items, customDurationSeconds);
        }

        public void TriggerDeathBarrage(int? customCount = null, double? customDurationSeconds = null)
        {
            int count = customCount ?? DanmakuSettingsStore.Count;
            IReadOnlyList<string> items = DanmakuRepository.GetRandomDeathBatch(count);
            TriggerBarrageInternal(items, customDurationSeconds);
        }

        public void TriggerBarrage(int? customCount = null, double? customDurationSeconds = null)
        {
            int count = customCount ?? DanmakuSettingsStore.Count;
            IReadOnlyList<string> items = DanmakuRepository.GetRandomBatch(count);
            TriggerBarrageInternal(items, customDurationSeconds);
        }

        private void TriggerBarrageInternal(IReadOnlyList<string> items, double? customDurationSeconds = null)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            double totalDurationSeconds = customDurationSeconds ?? DanmakuSettingsStore.DurationSeconds;
            DanmakuDisplayArea area = DanmakuSettingsStore.Area;
            int fontSize = DanmakuSettingsStore.FontSize;
            FontWeight fontWeight = DanmakuSettingsStore.ResolveFontWeight(DanmakuSettingsStore.FontWeight);
            bool showBackground = DanmakuSettingsStore.ShowBackground;
            bool showOutline = DanmakuSettingsStore.ShowOutline;
            DanmakuSpeedMode speed = DanmakuSettingsStore.Speed;

            double canvasWidth = ActualWidth > 50 ? ActualWidth : (Window.Current?.Bounds.Width ?? 1920);
            double canvasHeight = ActualHeight > 50 ? ActualHeight : (Window.Current?.Bounds.Height ?? 1080);

            // Update format cache
            if (_cachedTextFormat == null || _cachedFontSize != fontSize || _cachedFontWeight.Weight != fontWeight.Weight)
            {
                _cachedTextFormat?.Dispose();
                _cachedFontSize = fontSize;
                _cachedFontWeight = fontWeight;
                _cachedTextFormat = new CanvasTextFormat
                {
                    FontFamily = "Microsoft YaHei, Segoe UI",
                    FontSize = fontSize,
                    FontWeight = fontWeight,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Top,
                    WordWrapping = CanvasWordWrapping.NoWrap
                };
            }

            _cachedShowBackground = showBackground;
            _cachedShowOutline = showOutline;

            var validYRanges = ResolveYRangesForArea(area, canvasHeight);

            if (!_animationStopwatch.IsRunning)
            {
                _animationStopwatch.Restart();
                _lastFrameMs = 0;
            }
            long baseMs = _animationStopwatch.ElapsedMilliseconds;

            lock (_pendingList)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    string text = items[i];
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    double delaySec = _rand.NextDouble() * totalDurationSeconds;
                    long triggerMs = baseMs + (long)(delaySec * 1000.0);

                    float posY = (float)PickRandomYFromRanges(validYRanges);
                    double flightDuration = ResolveFlightDuration(speed);
                    float speedPxPerSec = (float)((canvasWidth + 600) / flightDuration);

                    _pendingList.Add(new PendingDanmaku
                    {
                        Text = text,
                        TargetY = posY,
                        SpeedPxPerSec = speedPxPerSec,
                        TriggerTimeMs = triggerMs,
                        Color = GetRandomDanmakuColor()
                    });
                }

                _pendingList.Sort((a, b) => a.TriggerTimeMs.CompareTo(b.TriggerTimeMs));
            }

            StartRendering();
        }

        private void StartRendering()
        {
            if (!_isRendering)
            {
                _isRendering = true;
                _lastFrameMs = _animationStopwatch.ElapsedMilliseconds;
                CompositionTarget.Rendering += OnCompositionRendering;
            }
        }

        private void StopRendering()
        {
            if (_isRendering)
            {
                _isRendering = false;
                CompositionTarget.Rendering -= OnCompositionRendering;
                _animationStopwatch.Stop();
                DanmakuCanvas.Invalidate();
            }
        }

        private void OnCompositionRendering(object sender, object e)
        {
            long nowMs = _animationStopwatch.ElapsedMilliseconds;
            float dt = (nowMs - _lastFrameMs) / 1000.0f;
            _lastFrameMs = nowMs;

            if (dt <= 0 || dt > 0.1f)
            {
                dt = 0.0166f; // Clamp delta time to avoid jumps
            }

            double canvasWidth = ActualWidth > 50 ? ActualWidth : (Window.Current?.Bounds.Width ?? 1920);

            // 1. Move active items
            lock (_activeList)
            {
                for (int i = _activeList.Count - 1; i >= 0; i--)
                {
                    var d = _activeList[i];
                    d.X -= d.SpeedPxPerSec * dt;

                    // Remove if completely off-screen left
                    if (d.X < -d.MeasuredWidth - 50)
                    {
                        _activeList.RemoveAt(i);
                    }
                }
            }

            // 2. Spawn pending items
            lock (_pendingList)
            {
                while (_pendingList.Count > 0 && _pendingList[0].TriggerTimeMs <= nowMs)
                {
                    var p = _pendingList[0];
                    _pendingList.RemoveAt(0);

                    // Estimate/measure width: ~ fontSize * 0.95 per character
                    float estimatedWidth = Math.Max(40, p.Text.Length * (_cachedFontSize * 0.95f));

                    lock (_activeList)
                    {
                        _activeList.Add(new ActiveDanmaku
                        {
                            Text = p.Text,
                            X = (float)canvasWidth + 10f,
                            Y = p.TargetY,
                            SpeedPxPerSec = p.SpeedPxPerSec,
                            MeasuredWidth = estimatedWidth,
                            Color = p.Color
                        });
                    }
                }
            }

            // 3. Check if all finished
            bool hasActive;
            bool hasPending;
            lock (_activeList) { hasActive = _activeList.Count > 0; }
            lock (_pendingList) { hasPending = _pendingList.Count > 0; }

            if (!hasActive && !hasPending)
            {
                StopRendering();
                return;
            }

            // 4. Request GPU Redraw
            DanmakuCanvas.Invalidate();
        }

        private void OnDanmakuCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var session = args.DrawingSession;
            session.Clear(Colors.Transparent);

            var format = _cachedTextFormat;
            if (format == null)
            {
                return;
            }

            List<ActiveDanmaku> snapshot;
            lock (_activeList)
            {
                if (_activeList.Count == 0)
                {
                    return;
                }
                snapshot = new List<ActiveDanmaku>(_activeList);
            }

            bool showBg = _cachedShowBackground;
            bool showOutline = _cachedShowOutline;
            float fontSize = _cachedFontSize;

            for (int i = 0; i < snapshot.Count; i++)
            {
                var d = snapshot[i];

                if (showBg)
                {
                    session.FillRoundedRectangle(
                        new Rect(d.X - 6, d.Y - 2, d.MeasuredWidth + 12, fontSize + 6),
                        4, 4,
                        ShadowBgColor);
                }

                if (showOutline)
                {
                    // 8-directional GPU Direct2D text outline
                    session.DrawText(d.Text, d.X - 1.2f, d.Y - 1.2f, TextOutlineColor, format);
                    session.DrawText(d.Text, d.X + 1.2f, d.Y - 1.2f, TextOutlineColor, format);
                    session.DrawText(d.Text, d.X - 1.2f, d.Y + 1.2f, TextOutlineColor, format);
                    session.DrawText(d.Text, d.X + 1.2f, d.Y + 1.2f, TextOutlineColor, format);
                    session.DrawText(d.Text, d.X - 1.4f, d.Y, TextOutlineColor, format);
                    session.DrawText(d.Text, d.X + 1.4f, d.Y, TextOutlineColor, format);
                    session.DrawText(d.Text, d.X, d.Y - 1.4f, TextOutlineColor, format);
                    session.DrawText(d.Text, d.X, d.Y + 1.4f, TextOutlineColor, format);
                }

                // Foreground text
                session.DrawText(d.Text, d.X, d.Y, d.Color, format);
            }
        }

        private static List<(double MinY, double MaxY)> ResolveYRangesForArea(DanmakuDisplayArea area, double height)
        {
            var ranges = new List<(double MinY, double MaxY)>();
            switch (area)
            {
                case DanmakuDisplayArea.Top:
                    ranges.Add((10, height * 0.48));
                    break;
                case DanmakuDisplayArea.Bottom:
                    ranges.Add((height * 0.52, height - 40));
                    break;
                case DanmakuDisplayArea.Center:
                    ranges.Add((height * 0.25, height * 0.75));
                    break;
                case DanmakuDisplayArea.AvoidCenter:
                    ranges.Add((10, height * 0.32));
                    ranges.Add((height * 0.68, height - 40));
                    break;
                case DanmakuDisplayArea.All:
                default:
                    ranges.Add((10, height - 40));
                    break;
            }
            return ranges;
        }

        private static double PickRandomYFromRanges(List<(double MinY, double MaxY)> ranges)
        {
            if (ranges == null || ranges.Count == 0)
            {
                return 50;
            }

            var range = ranges[_rand.Next(ranges.Count)];
            double minY = Math.Max(0, range.MinY);
            double maxY = Math.Max(minY + 20, range.MaxY);
            return minY + (_rand.NextDouble() * (maxY - minY));
        }

        private static double ResolveFlightDuration(DanmakuSpeedMode speed)
        {
            switch (speed)
            {
                case DanmakuSpeedMode.Fast:
                    return 2.9 + (_rand.NextDouble() * 0.8);
                case DanmakuSpeedMode.Slow:
                    return 5.6 + (_rand.NextDouble() * 1.5);
                case DanmakuSpeedMode.Normal:
                default:
                    return 4.0 + (_rand.NextDouble() * 1.2);
            }
        }

        private static Color GetRandomDanmakuColor()
        {
            int r = _rand.Next(100);
            if (r < 8)
            {
                return GoldColor;
            }
            if (r < 15)
            {
                return CyanColor;
            }
            return WhiteColor;
        }
    }
}
