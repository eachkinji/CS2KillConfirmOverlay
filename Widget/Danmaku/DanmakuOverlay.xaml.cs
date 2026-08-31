using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace KillConfirmGameBar.Danmaku
{
    public sealed partial class DanmakuOverlay : UserControl
    {
        private class ScheduledDanmakuItem
        {
            public string Text;
            public double TargetY;
            public double FlightDurationSec;
            public long TriggerTimeMs;
            public int FontSize;
            public FontWeight FontWeight;
            public bool ShowBackground;
            public bool ShowOutline;
        }

        private static readonly Random _rand = new Random();
        private const int MaxActiveElementsOnCanvas = 150;

        private static readonly SolidColorBrush WhiteBrush = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush GoldBrush = new SolidColorBrush(Color.FromArgb(255, 252, 211, 77));
        private static readonly SolidColorBrush CyanBrush = new SolidColorBrush(Color.FromArgb(255, 103, 232, 249));
        private static readonly SolidColorBrush ShadowBgBrush = new SolidColorBrush(Color.FromArgb(145, 0, 0, 0));
        private static readonly SolidColorBrush TextStrokeBrush = new SolidColorBrush(Color.FromArgb(235, 15, 15, 15));

        private readonly DispatcherTimer _scheduleDispatcherTimer = new DispatcherTimer();
        private readonly List<ScheduledDanmakuItem> _pendingItems = new List<ScheduledDanmakuItem>();
        private readonly Stopwatch _scheduleStopwatch = new Stopwatch();

        public DanmakuOverlay()
        {
            InitializeComponent();
            _scheduleDispatcherTimer.Interval = TimeSpan.FromMilliseconds(25);
            _scheduleDispatcherTimer.Tick += OnScheduleTimerTick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = DanmakuRepository.EnsureLoadedAsync();
            DanmakuSettingsStore.TestRequested -= OnTestRequested;
            DanmakuSettingsStore.TestRequested += OnTestRequested;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DanmakuSettingsStore.TestRequested -= OnTestRequested;
            _scheduleDispatcherTimer.Stop();
            _scheduleStopwatch.Stop();
            _pendingItems.Clear();
            BarrageCanvas.Children.Clear();
        }

        private void OnTestRequested()
        {
            TriggerBarrage();
        }

        public void TriggerBarrage(int? customCount = null, double? customDurationSeconds = null)
        {
            int count = customCount ?? DanmakuSettingsStore.Count;
            double totalDurationSeconds = customDurationSeconds ?? DanmakuSettingsStore.DurationSeconds;
            DanmakuDisplayArea area = DanmakuSettingsStore.Area;
            int fontSize = DanmakuSettingsStore.FontSize;
            FontWeight fontWeight = DanmakuSettingsStore.ResolveFontWeight(DanmakuSettingsStore.FontWeight);
            bool showBackground = DanmakuSettingsStore.ShowBackground;
            bool showOutline = DanmakuSettingsStore.ShowOutline;
            DanmakuSpeedMode speed = DanmakuSettingsStore.Speed;

            double canvasHeight = ActualHeight > 0 ? ActualHeight : (Window.Current?.Bounds.Height ?? 1080);
            if (canvasHeight <= 50)
            {
                canvasHeight = 1080;
            }

            IReadOnlyList<string> items = DanmakuRepository.GetRandomBatch(count);
            if (items == null || items.Count == 0)
            {
                return;
            }

            // Calculate valid Y ranges based on DisplayArea
            var validYRanges = ResolveYRangesForArea(area, canvasHeight);

            if (!_scheduleStopwatch.IsRunning)
            {
                _scheduleStopwatch.Restart();
            }
            long baseMs = _scheduleStopwatch.ElapsedMilliseconds;

            lock (_pendingItems)
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

                    // Pick random Y inside valid ranges
                    double posY = PickRandomYFromRanges(validYRanges);

                    // Flight duration based on speed
                    double flightSec = ResolveFlightDuration(speed);

                    _pendingItems.Add(new ScheduledDanmakuItem
                    {
                        Text = text,
                        TargetY = posY,
                        FlightDurationSec = flightSec,
                        TriggerTimeMs = triggerMs,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                        ShowBackground = showBackground,
                        ShowOutline = showOutline
                    });
                }

                // Sort by trigger time
                _pendingItems.Sort((a, b) => a.TriggerTimeMs.CompareTo(b.TriggerTimeMs));
            }

            if (!_scheduleDispatcherTimer.IsEnabled)
            {
                _scheduleDispatcherTimer.Start();
            }
        }

        private void OnScheduleTimerTick(object sender, object e)
        {
            long nowMs = _scheduleStopwatch.ElapsedMilliseconds;
            double canvasWidth = ActualWidth > 0 ? ActualWidth : (Window.Current?.Bounds.Width ?? 1920);
            if (canvasWidth <= 50)
            {
                canvasWidth = 1920;
            }

            List<ScheduledDanmakuItem> readyToSpawn = null;

            lock (_pendingItems)
            {
                int countToTake = 0;
                while (countToTake < _pendingItems.Count && _pendingItems[countToTake].TriggerTimeMs <= nowMs)
                {
                    countToTake++;
                    // Cap per-tick spawns to 2 items max to ensure 0 ms frame drops
                    if (countToTake >= 2)
                    {
                        break;
                    }
                }

                if (countToTake > 0)
                {
                    readyToSpawn = _pendingItems.GetRange(0, countToTake);
                    _pendingItems.RemoveRange(0, countToTake);
                }

                if (_pendingItems.Count == 0 && (readyToSpawn == null || readyToSpawn.Count == 0))
                {
                    _scheduleDispatcherTimer.Stop();
                    _scheduleStopwatch.Stop();
                }
            }

            if (readyToSpawn != null)
            {
                foreach (var item in readyToSpawn)
                {
                    SpawnSingleDanmaku(item, canvasWidth);
                }
            }
        }

        private void SpawnSingleDanmaku(ScheduledDanmakuItem item, double canvasWidth)
        {
            // Limit canvas capacity
            if (BarrageCanvas.Children.Count >= MaxActiveElementsOnCanvas)
            {
                BarrageCanvas.Children.RemoveAt(0);
            }

            Brush mainBrush = GetRandomDanmakuBrush();
            FrameworkElement contentElement;

            if (item.ShowOutline)
            {
                var outlineGrid = new Grid { IsHitTestVisible = false };
                var strokeBrush = TextStrokeBrush;

                // 8-directional outline offsets for crisp stroke
                double[,] offsets = new double[,] {
                    { -1.2, -1.2 }, { 1.2, -1.2 }, { -1.2, 1.2 }, { 1.2, 1.2 },
                    { -1.5, 0 }, { 1.5, 0 }, { 0, -1.5 }, { 0, 1.5 }
                };

                for (int i = 0; i < 8; i++)
                {
                    var outlineText = new TextBlock
                    {
                        Text = item.Text,
                        FontSize = item.FontSize,
                        FontWeight = item.FontWeight,
                        Foreground = strokeBrush,
                        TextWrapping = TextWrapping.NoWrap,
                        RenderTransform = new TranslateTransform { X = offsets[i, 0], Y = offsets[i, 1] }
                    };
                    outlineGrid.Children.Add(outlineText);
                }

                var mainText = new TextBlock
                {
                    Text = item.Text,
                    FontSize = item.FontSize,
                    FontWeight = item.FontWeight,
                    Foreground = mainBrush,
                    TextWrapping = TextWrapping.NoWrap
                };
                outlineGrid.Children.Add(mainText);
                contentElement = outlineGrid;
            }
            else
            {
                contentElement = new TextBlock
                {
                    Text = item.Text,
                    FontSize = item.FontSize,
                    FontWeight = item.FontWeight,
                    Foreground = mainBrush,
                    TextWrapping = TextWrapping.NoWrap
                };
            }

            FrameworkElement renderElement;
            if (item.ShowBackground)
            {
                renderElement = new Border
                {
                    Background = ShadowBgBrush,
                    Padding = new Thickness(7, 2, 7, 2),
                    CornerRadius = new CornerRadius(4),
                    Child = contentElement,
                    IsHitTestVisible = false
                };
            }
            else
            {
                renderElement = contentElement;
            }

            var transform = new TranslateTransform
            {
                X = canvasWidth,
                Y = item.TargetY
            };
            renderElement.RenderTransform = transform;

            Canvas.SetLeft(renderElement, 0);
            Canvas.SetTop(renderElement, 0);

            var anim = new DoubleAnimation
            {
                From = canvasWidth,
                To = -700,
                Duration = new Duration(TimeSpan.FromSeconds(item.FlightDurationSec)),
                EnableDependentAnimation = true
            };

            var storyboard = new Storyboard();
            Storyboard.SetTarget(anim, transform);
            Storyboard.SetTargetProperty(anim, "X");
            storyboard.Children.Add(anim);

            storyboard.Completed += (s, e) =>
            {
                storyboard.Stop();
                BarrageCanvas.Children.Remove(renderElement);
            };

            BarrageCanvas.Children.Add(renderElement);
            storyboard.Begin();
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
                    // Top 0% ~ 32% and Bottom 68% ~ 100%, leaving center 32% ~ 68% totally empty
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

        private static Brush GetRandomDanmakuBrush()
        {
            int r = _rand.Next(100);
            if (r < 8)
            {
                return GoldBrush;
            }
            if (r < 15)
            {
                return CyanBrush;
            }
            return WhiteBrush;
        }
    }
}
