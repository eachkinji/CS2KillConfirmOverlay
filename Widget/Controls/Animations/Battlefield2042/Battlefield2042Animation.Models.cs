using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void ResetBattlefield2042HudState()
        {
            _isBattlefield2042HudActive = false;
            _battlefield2042HudState.Clear();
            foreach (Battlefield2042KillIconRenderCache cache in _battlefield2042KillIconRenderCaches.Values)
            {
                cache?.Dispose();
            }

            _battlefield2042KillIconRenderCaches.Clear();
        }

        private sealed class Battlefield2042HudState
        {
            public readonly List<Battlefield2042FeedItem> FeedItems = new List<Battlefield2042FeedItem>();
            public readonly List<Battlefield2042MoneyItem> MoneyItems = new List<Battlefield2042MoneyItem>();
            public readonly List<Battlefield2042KillIconItem> KillIconItems = new List<Battlefield2042KillIconItem>();
            public double KillLogExpiresAtMs = -1;
            public double LastKillLogTriggerTimeMs = -1;
            public bool ExitSequenceStarted;
            public int PlayerKillfeedQueue;
            public int KillstreakQueue;
            public int IconGeneration;
            public int RoundNumber = -1;
            public int MoneyEpoch = -1;

            public void ResetScope(int roundNumber, int moneyEpoch)
            {
                ClearKillLog();
                RoundNumber = roundNumber;
                MoneyEpoch = moneyEpoch;
            }

            public void CompleteExitSequence()
            {
                KillIconItems.Clear();
                KillLogExpiresAtMs = -1;
                LastKillLogTriggerTimeMs = -1;
                ExitSequenceStarted = false;
                PlayerKillfeedQueue = 0;
                KillstreakQueue = 0;
                IconGeneration++;
            }

            public void ClearKillLog()
            {
                for (int i = 0; i < FeedItems.Count; i++)
                {
                    FeedItems[i].DisposeCachedResources();
                }

                for (int i = 0; i < MoneyItems.Count; i++)
                {
                    MoneyItems[i].DisposeCachedResources();
                }

                FeedItems.Clear();
                MoneyItems.Clear();
                CompleteExitSequence();
            }

            public void Clear()
            {
                ClearKillLog();
                RoundNumber = -1;
                MoneyEpoch = -1;
            }
        }
        private struct Battlefield2042CurveKey
        {
            public Battlefield2042CurveKey(
                double timeMs,
                double a,
                double b,
                double c,
                double d)
            {
                TimeMs = timeMs;
                A = a;
                B = b;
                C = c;
                D = d;
            }

            public double TimeMs { get; }
            public double A { get; }
            public double B { get; }
            public double C { get; }
            public double D { get; }
        }

        private struct Battlefield2042LegacyCurveKey
        {
            public Battlefield2042LegacyCurveKey(
                double timeMs,
                double value,
                double inSlope,
                double outSlope)
            {
                TimeMs = timeMs;
                Value = value;
                InSlope = inSlope;
                OutSlope = outSlope;
            }

            public double TimeMs { get; }
            public double Value { get; }
            public double InSlope { get; }
            public double OutSlope { get; }
        }
        private struct Battlefield2042GlitchBar
        {
            public Battlefield2042GlitchBar(double x, double y, double width, double height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public double X { get; }
            public double Y { get; }
            public double Width { get; }
            public double Height { get; }
        }

        private sealed class Battlefield2042KillIconItem
        {
            public Battlefield2042KillIconItem(
                string iconFileName,
                bool isHeadshot,
                bool isAssist,
                double revealTimeMs)
            {
                IconFileName = iconFileName;
                IsHeadshot = isHeadshot;
                IsAssist = isAssist;
                RevealTimeMs = revealTimeMs;
            }

            public string IconFileName { get; }
            public bool IsHeadshot { get; }
            public bool IsAssist { get; }
            public double RevealTimeMs { get; }
            public CanvasBitmap Icon { get; set; }
            public Battlefield2042KillIconRenderCache RenderCache { get; set; }
        }

        private sealed class Battlefield2042FeedItem
        {
            public Battlefield2042FeedItem(
                string targetName,
                string weaponName,
                bool isAssist,
                int moneyReward,
                double revealTimeMs)
            {
                TargetName = string.IsNullOrWhiteSpace(targetName) ? "ENEMY" : targetName;
                WeaponName = string.IsNullOrWhiteSpace(weaponName) ? "UNKNOWN" : weaponName;
                IsAssist = isAssist;
                MoneyReward = NormalizeBattlefieldMoneyReward(moneyReward);
                RevealTimeMs = revealTimeMs;
            }

            public string TargetName { get; }
            public string WeaponName { get; }
            public bool IsAssist { get; }
            public int MoneyReward { get; }
            public double RevealTimeMs { get; }
            public double ExitStartTimeMs { get; private set; } = -1;
            public bool IsExiting => ExitStartTimeMs >= 0;
            public bool IsCachePrepared { get; set; }
            public string WeaponText { get; set; }
            public string FullText { get; set; }
            public string MoneyText { get; set; }
            public Rect TextBounds { get; set; }
            public double WeaponAdvance { get; set; }
            public double MoneyTextWidth { get; set; }
            public Battlefield2042GlowCache WeaponTextGlow { get; set; }
            public Battlefield2042GlowCache TargetTextGlow { get; set; }
            public Battlefield2042GlowCache WeaponBackgroundGlow { get; set; }
            public Battlefield2042GlowCache TargetBackgroundGlow { get; set; }

            public void StartExit(double startTimeMs)
            {
                if (!IsExiting)
                {
                    ExitStartTimeMs = startTimeMs;
                }
            }

            public void DisposeCachedResources()
            {
                WeaponTextGlow?.Dispose();
                WeaponTextGlow = null;
                TargetTextGlow?.Dispose();
                TargetTextGlow = null;
                WeaponBackgroundGlow?.Dispose();
                WeaponBackgroundGlow = null;
                TargetBackgroundGlow?.Dispose();
                TargetBackgroundGlow = null;
            }
        }
        private sealed class Battlefield2042MoneyItem
        {
            public Battlefield2042MoneyItem(int moneyReward, double revealTimeMs)
            {
                MoneyReward = NormalizeBattlefieldMoneyReward(moneyReward);
                RevealTimeMs = revealTimeMs;
            }

            public int MoneyReward { get; }
            public double RevealTimeMs { get; }
            public double ExitStartTimeMs { get; private set; } = -1;
            public bool IsExiting => ExitStartTimeMs >= 0;
            public bool IsCachePrepared { get; set; }
            public string Text { get; set; }
            public Rect TextBounds { get; set; }
            public double TextWidth { get; set; }
            public Battlefield2042GlowCache TextGlow { get; set; }
            public Battlefield2042GlowCache BackgroundGlow { get; set; }

            public void StartExit(double startTimeMs)
            {
                if (!IsExiting)
                {
                    ExitStartTimeMs = startTimeMs;
                }
            }

            public void DisposeCachedResources()
            {
                TextGlow?.Dispose();
                TextGlow = null;
                BackgroundGlow?.Dispose();
                BackgroundGlow = null;
            }
        }

        private sealed class Battlefield2042GlowCache : IDisposable
        {
            public Battlefield2042GlowCache(CanvasRenderTarget surface, double offsetX, double offsetY)
            {
                Surface = surface;
                OffsetX = offsetX;
                OffsetY = offsetY;
            }

            public CanvasRenderTarget Surface { get; private set; }
            public double OffsetX { get; }
            public double OffsetY { get; }

            public void Dispose()
            {
                Surface?.Dispose();
                Surface = null;
            }
        }

        private sealed class Battlefield2042KillIconRenderCache : IDisposable
        {
            public Battlefield2042KillIconRenderCache(
                CanvasRenderTarget baseSurface,
                CanvasRenderTarget bloomSurface,
                double centerOffset)
            {
                BaseSurface = baseSurface;
                BloomSurface = bloomSurface;
                CenterOffset = centerOffset;
            }

            public CanvasRenderTarget BaseSurface { get; private set; }
            public CanvasRenderTarget BloomSurface { get; private set; }
            public double CenterOffset { get; }

            public void Dispose()
            {
                BaseSurface?.Dispose();
                BaseSurface = null;
                BloomSurface?.Dispose();
                BloomSurface = null;
            }
        }
    }
}
