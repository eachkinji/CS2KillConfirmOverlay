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
        private const double Battlefield2042FrameWidth = 607;
        private const double Battlefield2042FrameHeight = 260;
        private const double Battlefield2042KillLogDurationMs = 3170;
        private const double Battlefield2042SameFrameWindowMs = 34;
        private const double Battlefield2042QueueDelayMs = 200;
        private const double Battlefield2042KillstreakEntryMs = 466.6667;
        private const double Battlefield2042FeedEffectDurationMs = 2833.3333;
        private const double Battlefield2042FeedExitLeadMs = 70;
        private const double Battlefield2042FeedExitDurationMs = 280;
        private const double Battlefield2042FeedExitStaggerMs = 55;
        private const double Battlefield2042FeedObjectWidth = 389.5799866;
        private const double Battlefield2042FeedObjectHeight = 22.1753998;
        private const double Battlefield2042MoneyFeedLeftOffset = 48;
        private const double Battlefield2042FeedMoneyGap = 20;
        private const double Battlefield2042MoneyCursorWidth = 14.4842;
        private const double Battlefield2042FeedRowRightOffset = 112;
        private const double Battlefield2042MoneyCursorGap = 2;


        private const double Battlefield2042KillIconSize = 30;
        private const double Battlefield2042KillIconSlotWidth = 31.25;
        private const double Battlefield2042KillIconCenterY = 125;
        private const double Battlefield2042IconFeedVisualGap = 8;
        private const double Battlefield2042FeedCursorHalfHeight = 12.65;

        private const double Battlefield2042FeedBaseY =
            Battlefield2042KillIconCenterY + Battlefield2042KillIconSize / 2.0
            + Battlefield2042IconFeedVisualGap + Battlefield2042FeedCursorHalfHeight
            - Battlefield2042FeedObjectHeight / 2.0;
        private const double Battlefield2042MoneyTotalY = Battlefield2042KillIconCenterY + 24;
        private const double Battlefield2042FeedLineSpacing = 20;
        private const int Battlefield2042MaxFeedLines = 5;
        private const int Battlefield2042MaxKillIcons = 10;
        private static readonly Color Battlefield2042EnemyColor = Color.FromArgb(255, 255, 20, 24);
        private static readonly Color Battlefield2042HeadshotColor = Color.FromArgb(248, 255, 31, 1);
        private static readonly Color Battlefield2042HeadshotHaloColor = Color.FromArgb(255, 255, 84, 61);
        private static readonly Color Battlefield2042KilllogGlitchColor = Color.FromArgb(255, 255, 84, 61);
        private static readonly Vector2[] Battlefield2042BloomInnerOffsets =
        {
            new Vector2(1.35f, 0), new Vector2(-1.35f, 0),
            new Vector2(0, 1.35f), new Vector2(0, -1.35f)
        };
        private static readonly Vector2[] Battlefield2042BloomDiagonalOffsets =
        {
            new Vector2(1.9f, 1.9f), new Vector2(-1.9f, 1.9f),
            new Vector2(1.9f, -1.9f), new Vector2(-1.9f, -1.9f)
        };
        private static readonly Vector2[] Battlefield2042BloomOuterOffsets =
        {
            new Vector2(3.8f, 0), new Vector2(-3.8f, 0),
            new Vector2(0, 3.8f), new Vector2(0, -3.8f)
        };
        private static readonly Dictionary<string, CanvasBitmap> Battlefield2042IconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);

        private static readonly Battlefield2042CurveKey[] Battlefield2042AnimSkullAlphaCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 0),
            new Battlefield2042CurveKey(50, -129599.9375, 3239.999023, 0, 0),
            new Battlefield2042CurveKey(66.6667, 0, 0, 0, 0.3),
            new Battlefield2042CurveKey(166.6667, 0, 0, 0, 0.3),
            new Battlefield2042CurveKey(266.6667, 16200.00293, -810.000061, 0, 0.3),
            new Battlefield2042CurveKey(300, 0, 0, 0, 0),
            new Battlefield2042CurveKey(450, 0, 0, 0, 0)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042AnimSkullSizeCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 50),
            new Battlefield2042CurveKey(50, 0, 0, 0, 50),
            new Battlefield2042CurveKey(66.6667, 0, 0, 0, 50),
            new Battlefield2042CurveKey(150, 11851.850586, -2666.666504, 0, 50),
            new Battlefield2042CurveKey(300, 0, 0, 0, 30),
            new Battlefield2042CurveKey(450, 0, 0, 0, 30)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042AnimSkullXCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 2.94),
            new Battlefield2042CurveKey(50, 0, 0, 0, 2.94),
            new Battlefield2042CurveKey(66.6667, 0, 0, 0, 2.94),
            new Battlefield2042CurveKey(150, 0, 0, -54.60001, 2.94),
            new Battlefield2042CurveKey(166.6667, 0, 0, -97.199997, 2.03),
            new Battlefield2042CurveKey(216.6667, 0, 0.000191, 76.399979, -2.83),
            new Battlefield2042CurveKey(266.6667, 0, 0, -29.700003, 0.99),
            new Battlefield2042CurveKey(300, 0, 0, 1.767213, 0),
            new Battlefield2042CurveKey(450, 0, 0, 0, 0.265082)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042IconGlitchAXCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 0),
            new Battlefield2042CurveKey(250, 0, 0, 0, 0),
            new Battlefield2042CurveKey(283.3333, -1719355.625, 42983.929688, 0, 0),
            new Battlefield2042CurveKey(300, 676.279053, -16.90696, 0, 3.98),
            new Battlefield2042CurveKey(316.6667, 0, 0, 0, 3.978435),
            new Battlefield2042CurveKey(366.6667, 3191812.5, -79795.234375, 0, 3.978435),
            new Battlefield2042CurveKey(383.3333, -212.89447, 21.289446, 0, -3.41),
            new Battlefield2042CurveKey(450, 0, 0, 0, -3.37846)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042IconGlitchAYCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 0),
            new Battlefield2042CurveKey(250, 0, 0, 0, 0),
            new Battlefield2042CurveKey(366.6667, 1118883, -27972.048828, 0, 0),
            new Battlefield2042CurveKey(383.3333, -161.700211, 16.170019, 0, -2.59),
            new Battlefield2042CurveKey(450, 0, 0, 0, -2.566044)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042IconGlitchBXCurve =
        {
            new Battlefield2042CurveKey(0, 427.407349, -192.333313, 0, 0),
            new Battlefield2042CurveKey(300, 0, 0, 0, -5.77),
            new Battlefield2042CurveKey(383.3333, -3499191.25, 87479.859375, 0, -5.77),
            new Battlefield2042CurveKey(400, 0, 0, 0, 2.33),
            new Battlefield2042CurveKey(416.6667, -1343516.625, 33587.941406, 0, 2.33),
            new Battlefield2042CurveKey(433.3333, 0, 0, 0, 5.44),
            new Battlefield2042CurveKey(450, 0, 0, 0, 5.44)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042IconGlitchBYCurve =
        {
            new Battlefield2042CurveKey(0, -102.222214, 45.999996, 0, 0),
            new Battlefield2042CurveKey(300, 0, 0, 0, 1.38),
            new Battlefield2042CurveKey(383.3333, -475198.8125, 11879.980469, 0, 1.38),
            new Battlefield2042CurveKey(400, 0, 0, 0, 2.48),
            new Battlefield2042CurveKey(416.6667, 2039035, -50975.917969, 0, 2.48),
            new Battlefield2042CurveKey(433.3333, 0, 0, 0, -2.24),
            new Battlefield2042CurveKey(450, 0, 0, 0, -2.24)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042IconGlitchBarsA =
        {
            new Battlefield2042GlitchBar(3.10, 8.90, 41.61, 1.55),
            new Battlefield2042GlitchBar(0.32, 1.08, 41.61, 1.55),
            new Battlefield2042GlitchBar(-1.14, -11.23, 50.49, 0.72)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042IconGlitchBarsB =
        {
            new Battlefield2042GlitchBar(-4.20, 9.75, 41.88, 1.21),
            new Battlefield2042GlitchBar(-4.20, -3.00, 41.88, 1.21),
            new Battlefield2042GlitchBar(6.30, 12.60, 30.85, 2.94)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042KilllogGlitchBarsA =
        {
            new Battlefield2042GlitchBar(17.43, -43.10, 330.70, 3.27),
            new Battlefield2042GlitchBar(29.30, -47.50, 111.60, 3.27),
            new Battlefield2042GlitchBar(17.43, 4.00, 330.70, 3.27),
            new Battlefield2042GlitchBar(78.70, 50.20, 78.90, 1.90),
            new Battlefield2042GlitchBar(-121.34, 26.70, 132.41, 3.27),
            new Battlefield2042GlitchBar(62.80, -13.20, 26.48, 2.00),
            new Battlefield2042GlitchBar(-151.30, -32.00, 1.20, 11.98),
            new Battlefield2042GlitchBar(158.60, -64.60, 58.60, 2.74),
            new Battlefield2042GlitchBar(-164.60, 0.30, 1.17, 24.32)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042KilllogGlitchBarsB =
        {
            new Battlefield2042GlitchBar(2.10, -10.20, 368.60, 3.27),
            new Battlefield2042GlitchBar(183.65, -18.10, 5.50, 1.90),
            new Battlefield2042GlitchBar(183.65, -37.10, 5.50, 1.90),
            new Battlefield2042GlitchBar(2.10, -2.10, 368.60, 3.27),
            new Battlefield2042GlitchBar(-183.60, -14.30, 15.50, 1.90),
            new Battlefield2042GlitchBar(-183.60, -32.30, 15.50, 1.90),
            new Battlefield2042GlitchBar(55.70, -62.50, 220.92, 3.27),
            new Battlefield2042GlitchBar(-94.60, 32.00, 76.22, 3.27),
            new Battlefield2042GlitchBar(174.90, 39.00, 1.16, 42.36)
        };

        // Legacy AnimationClip "PlayerKillfeed" from the original RFC.
        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedRootAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(200, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(233.3333, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(366.6667, 1, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, 1, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedMaskPaddingXCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 120, 0, 0),
            new Battlefield2042LegacyCurveKey(233.3333, 120, 0, 0),
            new Battlefield2042LegacyCurveKey(383.3333, -23.5258331, -484.9429932, -484.9429932),
            new Battlefield2042LegacyCurveKey(400, -43.7317963, -969.8861084, -969.8861084),
            new Battlefield2042LegacyCurveKey(466.6667, -150, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, -150, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedMaskPaddingYCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 5, 0, 0),
            new Battlefield2042LegacyCurveKey(233.3333, 5, 0, 0),
            new Battlefield2042LegacyCurveKey(383.3333, -77.3944473, -278.393158, -278.393158),
            new Battlefield2042LegacyCurveKey(400, -88.9941635, -556.786316, -556.786316),
            new Battlefield2042LegacyCurveKey(466.6667, -150, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, -150, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedTextAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(400, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(766.6667, 1, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, 1, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedBackgroundAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(200, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(366.6667, 0.2, 1.2499985, 1.2499985),
            new Battlefield2042LegacyCurveKey(383.3333, 0.2572916, 4.5, 4.5),
            new Battlefield2042LegacyCurveKey(400, 0.35, 4.4500031, 4.4500031),
            new Battlefield2042LegacyCurveKey(466.6667, 0.92, 0, 0),
            new Battlefield2042LegacyCurveKey(766.6667, 0.3, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, 0.3, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedCursorAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(200, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(366.6667, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(383.3333, 0.03478998, 4.174798, 4.174798),
            new Battlefield2042LegacyCurveKey(400, 0.1391602, 8.3496122, 8.3496122),
            new Battlefield2042LegacyCurveKey(433.3333, 0.6, 0, 0),
            new Battlefield2042LegacyCurveKey(683.3333, 0.6, 0, 0),
            new Battlefield2042LegacyCurveKey(850, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, 0, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedCursorXCurve =
        {
            new Battlefield2042LegacyCurveKey(0, -15.3274994, 0, 0),
            new Battlefield2042LegacyCurveKey(200, -15.3274994, -812.01416, -812.01416),
            new Battlefield2042LegacyCurveKey(366.6667, -135.2265015, -472.30777, -472.30777),
            new Battlefield2042LegacyCurveKey(383.3333, -143.1312561, -271.0202, -271.0202),
            new Battlefield2042LegacyCurveKey(483.3333, -173.5233154, 0, 0),
            new Battlefield2042LegacyCurveKey(616.6667, -119.1142502, 888.0968, 888.0968),
            new Battlefield2042LegacyCurveKey(866.6667, 194.1999969, -0.9684653, -0.9684653)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedCursorWidthCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 11.9911041, 0, 0),
            new Battlefield2042LegacyCurveKey(200, 11.9911041, 0, 0),
            new Battlefield2042LegacyCurveKey(383.3333, 11.9911041, 0, 0),
            new Battlefield2042LegacyCurveKey(600, 11.9911041, 0, 0),
            new Battlefield2042LegacyCurveKey(716.6667, 224.9952698, 0, 0),
            new Battlefield2042LegacyCurveKey(816.6667, 53.2138672, -82.8958282, -82.8958282),
            new Battlefield2042LegacyCurveKey(866.6667, 50.1052742, 0, 0)
        };
        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042MoneyCursorAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(1116.6667, 0.8, 0.5647058, 0.5647058),
            new Battlefield2042LegacyCurveKey(1416.6667, 1, 0, 0),
            new Battlefield2042LegacyCurveKey(1500, 1, 0, 0),
            new Battlefield2042LegacyCurveKey(1900, 0.8, 0, 0),
            new Battlefield2042LegacyCurveKey(2233.3333, 0.8, 0, 0),
            new Battlefield2042LegacyCurveKey(2833.3333, 0, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042MoneyCursorHeightCurve =
        {
            new Battlefield2042LegacyCurveKey(1116.6667, 22.1760006, 0, 0),
            new Battlefield2042LegacyCurveKey(1416.6667, 13, 0, 0),
            new Battlefield2042LegacyCurveKey(1500, 13, 0, 0),
            new Battlefield2042LegacyCurveKey(1900, 22.1760006, 0, 0),
            new Battlefield2042LegacyCurveKey(2233.3333, 22.1760006, 0, 0),
            new Battlefield2042LegacyCurveKey(2833.3333, 0, 0, 0)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042FeedGlitchBarsA =
        {
            new Battlefield2042GlitchBar(-44.699, 10.2, 27.212, 1.55),
            new Battlefield2042GlitchBar(-25.4, -16.5, 95.637, 1.441)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042FeedGlitchBarsB =
        {
            new Battlefield2042GlitchBar(-105.7, 6.5, 1.821, 20.228),
            new Battlefield2042GlitchBar(-1, -10.2, 144.486, 1.219),
            new Battlefield2042GlitchBar(54.1, 6.7, 130.76, 2.4),
            new Battlefield2042GlitchBar(-69.3, -1.221, 72.689, 2.42)
        };
        private readonly Battlefield2042HudState _battlefield2042HudState = new Battlefield2042HudState();
        private bool _isBattlefield2042HudActive;

        public void PlayBattlefield2042Kill(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponLabel,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch)
        {
            string normalizedEventKind = NormalizeBattlefieldEventKind(isAssist, eventKind);
            PrepareBattlefield2042HudPlayback();
            AddBattlefield2042Event(
                Math.Max(0, killCount),
                isHeadshot,
                isAssist,
                string.IsNullOrWhiteSpace(playerName) ? "ENEMY" : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                NormalizeBattlefieldMoneyReward(moneyReward),
                normalizedEventKind,
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch));
        }
        private async Task PreloadBattlefield2042AnimationsAsync(IProgress<int> progress)
        {
            string[] files =
            {
                "NormalSkullSprite.png",
                "HeadshotSkullSprite.png",
                "AssistSprite.png",

                "SmoothCircle.png",
                "Glitch0.png",
                "Glitch1.png",
                "Glitch2.png",
                "Glitch3.png",
                "Glitch4.png"
            };

            progress?.Report(0);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    await LoadBattlefield2042IconAsync(files[i]);
                }
                catch
                {
                }

                int percent = (int)Math.Round((i + 1) * 100.0 / files.Length);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
        }

        private static void ClearBattlefield2042IconCache()
        {
            Battlefield2042IconCache.Clear();
        }

        private static CanvasBitmap GetCachedBattlefield2042Icon(string iconFileName)
        {
            string cacheKey = "battlefield2042/" + iconFileName;
            lock (Battlefield2042IconCache)
            {
                Battlefield2042IconCache.TryGetValue(cacheKey, out CanvasBitmap cached);
                return cached;
            }
        }

        private static async Task<CanvasBitmap> LoadBattlefield2042IconAsync(string iconFileName)
        {
            string cacheKey = "battlefield2042/" + iconFileName;
            lock (Battlefield2042IconCache)
            {
                if (Battlefield2042IconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = await LoadBitmapFromApplicationUriAsync(
                "ms-appx:///Assets/GameStyles/battlefield2042/killconfirm/textures/" + iconFileName);

            lock (Battlefield2042IconCache)
            {
                if (Battlefield2042IconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }

                Battlefield2042IconCache[cacheKey] = loaded;
                return loaded;
            }
        }

        private void PrepareBattlefield2042HudPlayback()
        {
            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = false;
            _isBattlefield4HudActive = false;
            _isPubgHudActive = false;
            _isDeltaForceHudActive = false;
            _isBattlefield2042HudActive = true;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)Battlefield2042FrameWidth,
                FrameHeight = (int)Battlefield2042FrameHeight,
                Frames = (int)Math.Ceiling(Battlefield2042KillLogDurationMs / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(Battlefield2042FrameWidth, Battlefield2042FrameHeight);
            HideLoadingProgress();
            Visibility = Windows.UI.Xaml.Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            if (!_playbackClock.IsRunning)
            {
                _playbackClock.Restart();
            }

            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        private async void AddBattlefield2042Event(
            int killCount,
            bool isHeadshot,
            bool isAssist,
            string targetName,
            string weaponName,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch)
        {
            double now = _playbackClock.IsRunning ? _playbackClock.Elapsed.TotalMilliseconds : 0;
            EnsureBattlefield2042Scope(roundNumber, moneyEpoch);
            int reward = NormalizeBattlefieldMoneyReward(moneyReward);
            AddBattlefieldMoneyReward("bf2042", reward, roundNumber, moneyEpoch, now);

            bool textOnlyEvent = IsRoundBonusEvent(eventKind) || IsObjectiveBonusEvent(eventKind);
            bool sameFrameBurst = _battlefield2042HudState.KillLogExpiresAtMs > now
                && _battlefield2042HudState.LastKillLogTriggerTimeMs >= 0
                && now - _battlefield2042HudState.LastKillLogTriggerTimeMs <= Battlefield2042SameFrameWindowMs;

            double feedRevealTimeMs = now;
            if (!textOnlyEvent)
            {
                if (!isAssist)
                {
                    _battlefield2042HudState.PlayerKillfeedQueue++;
                    if (sameFrameBurst)
                    {
                        feedRevealTimeMs += Battlefield2042QueueDelayMs * _battlefield2042HudState.PlayerKillfeedQueue;
                    }
                }

                AddBattlefield2042FeedItem(new Battlefield2042FeedItem(
                    targetName,
                    isAssist ? string.Empty : weaponName,
                    isAssist,
                    reward,
                    feedRevealTimeMs),
                    now);
            }

            if (reward > 0)
            {
                AddBattlefield2042MoneyItem(new Battlefield2042MoneyItem(
                    reward,
                    feedRevealTimeMs),
                    now);
            }

            Battlefield2042KillIconItem killIconItem = null;
            if (!textOnlyEvent && _battlefield2042HudState.KillIconItems.Count < Battlefield2042MaxKillIcons)
            {
                _battlefield2042HudState.KillstreakQueue++;
                double iconRevealTimeMs = now;
                if (sameFrameBurst)
                {
                    iconRevealTimeMs += Battlefield2042QueueDelayMs * _battlefield2042HudState.KillstreakQueue;
                }

                killIconItem = new Battlefield2042KillIconItem(
                    GetBattlefield2042IconFileName(isHeadshot, isAssist),
                    isHeadshot,
                    isAssist,
                    iconRevealTimeMs);
                _battlefield2042HudState.KillIconItems.Add(killIconItem);
            }

            _battlefield2042HudState.ExitSequenceStarted = false;
            _battlefield2042HudState.KillLogExpiresAtMs = now + Battlefield2042KillLogDurationMs;
            _battlefield2042HudState.LastKillLogTriggerTimeMs = now;
            int iconGeneration = _battlefield2042HudState.IconGeneration;
            SpriteCanvas.Invalidate();

            if (killIconItem == null)
            {
                return;
            }

            CanvasBitmap icon = null;
            try
            {
                icon = await LoadBattlefield2042IconAsync(
                    GetBattlefield2042IconFileName(isHeadshot, isAssist));
            }
            catch
            {
            }

            if (icon != null
                && iconGeneration == _battlefield2042HudState.IconGeneration
                && _battlefield2042HudState.KillIconItems.Contains(killIconItem))
            {
                killIconItem.Icon = icon;
            }

            SpriteCanvas.Invalidate();
        }
        private static string GetBattlefield2042IconFileName(bool isHeadshot, bool isAssist)
        {
            if (isAssist)
            {
                return "AssistSprite.png";
            }

            return isHeadshot ? "HeadshotSkullSprite.png" : "NormalSkullSprite.png";
        }

        private void AddBattlefield2042FeedItem(Battlefield2042FeedItem item, double now)
        {
            _battlefield2042HudState.FeedItems.Add(item);
            int activeCount = 0;
            for (int i = 0; i < _battlefield2042HudState.FeedItems.Count; i++)
            {
                if (!_battlefield2042HudState.FeedItems[i].IsExiting)
                {
                    activeCount++;
                }
            }

            if (activeCount <= Battlefield2042MaxFeedLines)
            {
                return;
            }

            for (int i = 0; i < _battlefield2042HudState.FeedItems.Count; i++)
            {
                Battlefield2042FeedItem candidate = _battlefield2042HudState.FeedItems[i];
                if (!candidate.IsExiting)
                {
                    candidate.StartExit(now);
                    break;
                }
            }
        }

        private void AddBattlefield2042MoneyItem(Battlefield2042MoneyItem item, double now)
        {
            _battlefield2042HudState.MoneyItems.Add(item);
            int activeCount = 0;
            for (int i = 0; i < _battlefield2042HudState.MoneyItems.Count; i++)
            {
                if (!_battlefield2042HudState.MoneyItems[i].IsExiting)
                {
                    activeCount++;
                }
            }

            if (activeCount <= Battlefield2042MaxFeedLines)
            {
                return;
            }

            for (int i = 0; i < _battlefield2042HudState.MoneyItems.Count; i++)
            {
                Battlefield2042MoneyItem candidate = _battlefield2042HudState.MoneyItems[i];
                if (!candidate.IsExiting)
                {
                    candidate.StartExit(now);
                    break;
                }
            }
        }

        private void BeginBattlefield2042ExitSequence(double now)
        {
            _battlefield2042HudState.ExitSequenceStarted = true;
            double feedExitTime = now;
            for (int i = 0; i < _battlefield2042HudState.FeedItems.Count; i++)
            {
                Battlefield2042FeedItem item = _battlefield2042HudState.FeedItems[i];
                if (!item.IsExiting)
                {
                    item.StartExit(feedExitTime);
                    feedExitTime += Battlefield2042FeedExitStaggerMs;
                }
            }

            double moneyExitTime = now;
            for (int i = 0; i < _battlefield2042HudState.MoneyItems.Count; i++)
            {
                Battlefield2042MoneyItem item = _battlefield2042HudState.MoneyItems[i];
                if (!item.IsExiting)
                {
                    item.StartExit(moneyExitTime);
                    moneyExitTime += Battlefield2042FeedExitStaggerMs;
                }
            }
        }

        private void RemoveFinishedBattlefield2042Items(double now)
        {
            for (int i = _battlefield2042HudState.FeedItems.Count - 1; i >= 0; i--)
            {
                Battlefield2042FeedItem item = _battlefield2042HudState.FeedItems[i];
                if (item.IsExiting
                    && now >= item.ExitStartTimeMs + Battlefield2042FeedExitDurationMs)
                {
                    _battlefield2042HudState.FeedItems.RemoveAt(i);
                }
            }

            for (int i = _battlefield2042HudState.MoneyItems.Count - 1; i >= 0; i--)
            {
                Battlefield2042MoneyItem item = _battlefield2042HudState.MoneyItems[i];
                if (item.IsExiting
                    && now >= item.ExitStartTimeMs + Battlefield2042FeedExitDurationMs)
                {
                    _battlefield2042HudState.MoneyItems.RemoveAt(i);
                }
            }
        }
        private void EnsureBattlefield2042Scope(int roundNumber, int moneyEpoch)
        {
            if (_battlefield2042HudState.RoundNumber == roundNumber
                && _battlefield2042HudState.MoneyEpoch == moneyEpoch)
            {
                return;
            }

            _battlefield2042HudState.ResetScope(roundNumber, moneyEpoch);
        }

        private void UpdateBattlefield2042HudFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            if (_battlefield2042HudState.KillLogExpiresAtMs >= 0
                && !_battlefield2042HudState.ExitSequenceStarted
                && now >= _battlefield2042HudState.KillLogExpiresAtMs - Battlefield2042FeedExitLeadMs)
            {
                BeginBattlefield2042ExitSequence(now);
            }

            RemoveFinishedBattlefield2042Items(now);
            if (_battlefield2042HudState.ExitSequenceStarted
                && _battlefield2042HudState.FeedItems.Count == 0
                && _battlefield2042HudState.MoneyItems.Count == 0)
            {
                _battlefield2042HudState.CompleteExitSequence();
            }

            if (_battlefield2042HudState.FeedItems.Count == 0
                && _battlefield2042HudState.MoneyItems.Count == 0
                && _battlefield2042HudState.KillIconItems.Count == 0
                && !IsBattlefield5MoneyVisible(now))
            {
                ResetBattlefield2042HudState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }
        private void DrawBattlefield2042HudFrame(CanvasDrawingSession drawingSession)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Bahnschrift",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            })
            {

                DrawBattlefield2042KillIcons(drawingSession, now);
                DrawBattlefield2042Feed(drawingSession, textFormat, now);
                DrawBattlefield2042MoneyFeed(drawingSession, textFormat, now);
                DrawBattlefield2042MoneyTotal(drawingSession, textFormat, now);
                DrawBattlefield2042KilllogExitGlitch(drawingSession, now);
            }
        }

        private void DrawBattlefield2042KillIcons(CanvasDrawingSession drawingSession, double now)
        {
            int visibleCount = 0;
            for (int i = 0; i < _battlefield2042HudState.KillIconItems.Count; i++)
            {
                if (now >= _battlefield2042HudState.KillIconItems[i].RevealTimeMs)
                {
                    visibleCount++;
                }
            }

            if (visibleCount == 0)
            {
                return;
            }

            double firstCenterX = Battlefield2042FrameWidth / 2.0
                - ((visibleCount - 1) * Battlefield2042KillIconSlotWidth / 2.0);
            int visibleIndex = 0;
            for (int i = 0; i < _battlefield2042HudState.KillIconItems.Count; i++)
            {
                Battlefield2042KillIconItem item = _battlefield2042HudState.KillIconItems[i];
                double elapsed = now - item.RevealTimeMs;
                if (elapsed < 0)
                {
                    continue;
                }

                double centerX = firstCenterX + visibleIndex * Battlefield2042KillIconSlotWidth;
                DrawBattlefield2042KillIcon(
                    drawingSession,
                    item,
                    centerX,
                    Battlefield2042KillIconCenterY,
                    elapsed);
                visibleIndex++;
            }
        }

        private static void DrawBattlefield2042KillIcon(
            CanvasDrawingSession drawingSession,
            Battlefield2042KillIconItem item,
            double centerX,
            double centerY,
            double elapsed)
        {
            if (item.Icon == null)
            {
                return;
            }

            string glitchFrameName = GetBattlefield2042GlitchFrameName(elapsed);
            CanvasBitmap glitchFrame = glitchFrameName == null
                ? null
                : GetCachedBattlefield2042Icon(glitchFrameName);
            if (glitchFrame != null)
            {
                var maskRect = new Rect(centerX - 19, centerY - 15.4, 38, 30.8);
                var glitchRect = new Rect(
                    centerX - 40.18595,
                    centerY - 26.32505,
                    80.3719,
                    52.6501);
                using (drawingSession.CreateLayer(1.0f, maskRect))
                {
                    DrawBattlefield2042Image(drawingSession, glitchFrame, glitchRect, 0.27058825);
                }
            }

            CanvasBitmap shadow = GetCachedBattlefield2042Icon("SmoothCircle.png");
            if (shadow != null)
            {
                DrawBattlefield2042TintedImage(
                    drawingSession,
                    shadow,
                    new Rect(centerX - 20, centerY - 20, 40, 40),
                    Color.FromArgb(255, 0, 0, 0),
                    0.27058825);
            }

            Color skullColor = item.IsHeadshot ? Battlefield2042HeadshotColor : Colors.White;
            double skullOpacity = item.IsHeadshot ? Battlefield2042HeadshotColor.A / 255.0 : 1.0;
            var skullRect = new Rect(
                centerX - Battlefield2042KillIconSize / 2.0,
                centerY - Battlefield2042KillIconSize / 2.0,
                Battlefield2042KillIconSize,
                Battlefield2042KillIconSize);
            DrawBattlefield2042TintedImageWithBloom(
                drawingSession,
                item.Icon,
                skullRect,
                skullColor,
                skullOpacity,
                item.IsHeadshot ? 0.55 : 0.42);

            if (elapsed <= Battlefield2042KillstreakEntryMs)
            {
                double animAlpha = Clamp01(EvaluateBattlefield2042Curve(
                    Battlefield2042AnimSkullAlphaCurve,
                    elapsed));
                if (animAlpha > 0.0001)
                {
                    double animSize = EvaluateBattlefield2042Curve(
                        Battlefield2042AnimSkullSizeCurve,
                        elapsed);
                    double animX = EvaluateBattlefield2042Curve(
                        Battlefield2042AnimSkullXCurve,
                        elapsed);
                    Color animColor = item.IsHeadshot
                        ? Battlefield2042HeadshotHaloColor
                        : Colors.White;
                    var animRect = new Rect(
                        centerX + animX - animSize / 2.0,
                        centerY - animSize / 2.0,
                        animSize,
                        animSize);
                    DrawBattlefield2042TintedImageWithBloom(
                        drawingSession,
                        item.Icon,
                        animRect,
                        animColor,
                        animAlpha,
                        1.0);
                }

                if (elapsed >= 250 && elapsed < 416.6667)
                {
                    DrawBattlefield2042GlitchBars(
                        drawingSession,
                        Battlefield2042IconGlitchBarsA,
                        centerX + EvaluateBattlefield2042Curve(Battlefield2042IconGlitchAXCurve, elapsed),
                        centerY - EvaluateBattlefield2042Curve(Battlefield2042IconGlitchAYCurve, elapsed),
                        Colors.White,
                        0);
                }

                if (elapsed >= 300 && elapsed < 450)
                {
                    DrawBattlefield2042GlitchBars(
                        drawingSession,
                        Battlefield2042IconGlitchBarsB,
                        centerX + EvaluateBattlefield2042Curve(Battlefield2042IconGlitchBXCurve, elapsed),
                        centerY - EvaluateBattlefield2042Curve(Battlefield2042IconGlitchBYCurve, elapsed),
                        Colors.White,
                        item.IsHeadshot ? 8.92 : 0);
                }
            }
        }

        private static string GetBattlefield2042GlitchFrameName(double elapsed)
        {
            if (elapsed >= 166.6667 && elapsed < 183.3333)
            {
                return "Glitch0.png";
            }

            if (elapsed >= 183.3333 && elapsed < 250)
            {
                return "Glitch1.png";
            }

            if (elapsed >= 250 && elapsed < 266.6667)
            {
                return "Glitch0.png";
            }

            if (elapsed >= 316.6667 && elapsed < 333.3333)
            {
                return "Glitch2.png";
            }

            if (elapsed >= 333.3333 && elapsed < 366.6667)
            {
                return "Glitch3.png";
            }

            if (elapsed >= 366.6667 && elapsed < 383.3333)
            {
                return "Glitch1.png";
            }

            return null;
        }

        private static double EvaluateBattlefield2042Curve(
            Battlefield2042CurveKey[] keys,
            double elapsedMs)
        {
            if (keys == null || keys.Length == 0)
            {
                return 0;
            }

            int index = 0;
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                if (elapsedMs >= keys[i].TimeMs)
                {
                    index = i;
                    break;
                }
            }

            Battlefield2042CurveKey key = keys[index];
            double deltaSeconds = Math.Max(0, elapsedMs - key.TimeMs) / 1000.0;
            return ((key.A * deltaSeconds + key.B) * deltaSeconds + key.C) * deltaSeconds + key.D;
        }

        private static void DrawBattlefield2042GlitchBars(
            CanvasDrawingSession drawingSession,
            Battlefield2042GlitchBar[] bars,
            double centerX,
            double centerY,
            Color color,
            double finalHeightOverride)
        {
            if (bars == null)
            {
                return;
            }

            CanvasBlend previousBlend = drawingSession.Blend;
            try
            {
                drawingSession.Blend = CanvasBlend.Add;
                for (int i = 0; i < bars.Length; i++)
                {
                    Battlefield2042GlitchBar bar = bars[i];
                    double height = finalHeightOverride > 0 && i == bars.Length - 1
                        ? finalHeightOverride
                        : bar.Height;
                    var rect = new Rect(
                        centerX + bar.X - bar.Width / 2.0,
                        centerY - bar.Y - height / 2.0,
                        bar.Width,
                        height);
                    drawingSession.FillRectangle(
                        new Rect(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2),
                        Color.FromArgb(32, color.R, color.G, color.B));
                    drawingSession.FillRectangle(rect, color);
                }
            }
            finally
            {
                drawingSession.Blend = previousBlend;
            }
        }

        private static void DrawBattlefield2042TintedImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Color tint,
            double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            var tintEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateBattlefield2042AlphaTintMatrix(tint)
            };
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            drawingSession.DrawImage(
                tintEffect,
                target,
                source,
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);
        }

        private static void DrawBattlefield2042TintedImageWithBloom(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Color tint,
            double opacity,
            double bloomStrength)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            var tintEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateBattlefield2042AlphaTintMatrix(tint)
            };
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            CanvasBlend previousBlend = drawingSession.Blend;
            try
            {
                drawingSession.Blend = CanvasBlend.Add;
                double innerOpacity = opacity * bloomStrength * 0.16;
                double outerOpacity = opacity * bloomStrength * 0.075;
                double diagonalOpacity = opacity * bloomStrength * 0.10;
                foreach (Vector2 offset in Battlefield2042BloomInnerOffsets)
                {
                    drawingSession.DrawImage(
                        tintEffect,
                        OffsetBattlefield2042Rect(target, offset.X, offset.Y),
                        source,
                        (float)Clamp01(innerOpacity),
                        CanvasImageInterpolation.Linear);
                }

                foreach (Vector2 offset in Battlefield2042BloomDiagonalOffsets)
                {
                    drawingSession.DrawImage(
                        tintEffect,
                        OffsetBattlefield2042Rect(target, offset.X, offset.Y),
                        source,
                        (float)Clamp01(diagonalOpacity),
                        CanvasImageInterpolation.Linear);
                }

                foreach (Vector2 offset in Battlefield2042BloomOuterOffsets)
                {
                    drawingSession.DrawImage(
                        tintEffect,
                        OffsetBattlefield2042Rect(target, offset.X, offset.Y),
                        source,
                        (float)Clamp01(outerOpacity),
                        CanvasImageInterpolation.Linear);
                }
            }
            finally
            {
                drawingSession.Blend = previousBlend;
            }

            drawingSession.DrawImage(
                tintEffect,
                target,
                source,
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);
        }

        private static Matrix5x4 CreateBattlefield2042AlphaTintMatrix(Color tint)
        {
            return new Matrix5x4
            {
                M41 = tint.R / 255.0f,
                M42 = tint.G / 255.0f,
                M43 = tint.B / 255.0f,
                M44 = tint.A / 255.0f
            };
        }

        private static Rect OffsetBattlefield2042Rect(Rect rect, double x, double y)
        {
            return new Rect(rect.X + x, rect.Y + y, rect.Width, rect.Height);
        }
        private void DrawBattlefield2042Feed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            int row = 0;
            for (int i = _battlefield2042HudState.FeedItems.Count - 1; i >= 0; i--)
            {
                Battlefield2042FeedItem item = _battlefield2042HudState.FeedItems[i];
                double elapsed = now - item.RevealTimeMs;
                if (elapsed < 0)
                {
                    continue;
                }

                int visualRow = Math.Min(row, Battlefield2042MaxFeedLines - 1);
                double exitProgress = ResolveBattlefield2042ExitProgress(item.ExitStartTimeMs, now);
                double exitEase = EaseOutCubic(exitProgress);
                const double textScale = 1.02;
                string weaponText = item.IsAssist || string.IsNullOrWhiteSpace(item.WeaponName)
                    ? string.Empty
                    : "[" + item.WeaponName + "] ";
                string targetText = item.TargetName;
                string fullText = weaponText + targetText;
                string moneyText = item.MoneyReward > 0
                    ? "+" + FormatBattlefieldMoney(item.MoneyReward)
                    : string.Empty;
                Rect textBounds = MeasureBattlefieldTextBounds(fullText, textFormat);
                double weaponAdvance = MeasureBattlefieldTextAdvance(weaponText, textFormat);
                double moneyTextWidth = MeasureBattlefieldTextWidth(moneyText, textFormat) * textScale;
                double moneyX = ResolveBattlefield2042MoneyFeedX(moneyTextWidth, 0);
                double rightX = moneyX - Battlefield2042FeedMoneyGap + (42 * exitEase);
                double centerY = Battlefield2042FeedBaseY
                    + visualRow * Battlefield2042FeedLineSpacing
                    + Battlefield2042FeedObjectHeight / 2.0
                    + (7 * exitEase);
                double originX = rightX - ((textBounds.X + textBounds.Width) * textScale);
                double originY = centerY
                    - ((textBounds.Y + (textBounds.Height / 2.0)) * textScale);
                double x = originX + (textBounds.X * textScale);
                double totalWidth = textBounds.Width * textScale;
                double weaponWidth = weaponAdvance * textScale;
                double targetWidth = Math.Max(0, totalWidth - weaponWidth);
                double feedLeft = x - 3.5;
                double rowTextRight = item.MoneyReward > 0
                    ? moneyX + moneyTextWidth
                    : x + totalWidth;
                double cursorStopX = rowTextRight + Battlefield2042MoneyCursorGap;
                double cursorStopCenterX = cursorStopX + Battlefield2042MoneyCursorWidth / 2.0;
                double rootAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                    Battlefield2042FeedRootAlphaCurve,
                    elapsed)) * (1.0 - exitProgress);

                if (rootAlpha > 0.0001)
                {
                    Rect clip = LimitBattlefield2042FeedClip(
                        CreateBattlefield2042FeedClipRect(rightX, centerY, false, elapsed),
                        feedLeft - 6,
                        cursorStopX + Battlefield2042MoneyCursorWidth + 6);
                    using (drawingSession.CreateLayer((float)rootAlpha, clip))
                    {
                        double backgroundAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                            Battlefield2042FeedBackgroundAlphaCurve,
                            elapsed));
                        if (weaponWidth > 0.1)
                        {
                            DrawBattlefield2042GlowingRectangle(
                                drawingSession,
                                new Rect(
                                    x - 3.5,
                                    centerY - 6,
                                    weaponWidth + 4.5,
                                    12),
                                Color.FromArgb(255, 245, 249, 249),
                                backgroundAlpha,
                                0.58);
                        }

                        if (targetWidth > 0.1)
                        {
                            DrawBattlefield2042GlowingRectangle(
                                drawingSession,
                                new Rect(
                                    x + weaponWidth - 0.5,
                                    centerY - 6,
                                    targetWidth + 5,
                                    12),
                                Battlefield2042EnemyColor,
                                backgroundAlpha,
                                0.84);
                        }

                        double textAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                            Battlefield2042FeedTextAlphaCurve,
                            elapsed));
                        byte alpha = (byte)Math.Max(0, Math.Min(255, textAlpha * 255));
                        DrawBattlefield2042GlowingTextAtLayoutOrigin(
                            drawingSession,
                            weaponText,
                            originX,
                            originY,
                            textScale,
                            Color.FromArgb(alpha, 245, 249, 249),
                            0.72,
                            textFormat);
                        DrawBattlefield2042GlowingTextAtLayoutOrigin(
                            drawingSession,
                            targetText,
                            originX + (weaponAdvance * textScale),
                            originY,
                            textScale,
                            Color.FromArgb(
                                alpha,
                                Battlefield2042EnemyColor.R,
                                Battlefield2042EnemyColor.G,
                                Battlefield2042EnemyColor.B),
                            1.0,
                            textFormat);
                        DrawBattlefield2042FeedGlitches(
                            drawingSession,
                            elapsed,
                            rightX,
                            centerY);

                        if (elapsed <= Battlefield2042FeedEffectDurationMs)
                        {
                            double cursorAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042FeedCursorAlphaCurve,
                                elapsed));
                            double sourceCursorX = EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042FeedCursorXCurve,
                                elapsed);
                            double sourceCursorWidth = Math.Max(0, EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042FeedCursorWidthCurve,
                                elapsed));
                            double cursorProgress = Clamp01(
                                (sourceCursorX + 173.5233154)
                                / (194.1999969 + 173.5233154));
                            double cursorCenterX = Lerp(
                                feedLeft,
                                cursorStopCenterX,
                                cursorProgress);
                            double sourceSpan = 194.1999969 + 173.5233154;
                            double rowSpan = Math.Max(
                                Battlefield2042MoneyCursorWidth,
                                cursorStopCenterX - feedLeft);
                            double cursorWidth = Math.Max(
                                4,
                                sourceCursorWidth * Math.Min(1, rowSpan / sourceSpan));
                            double settleProgress = EaseOutCubic(Clamp01(
                                (elapsed - 716.6667) / 150.0));
                            cursorCenterX = Lerp(
                                cursorCenterX,
                                cursorStopCenterX,
                                settleProgress);
                            cursorWidth = Lerp(
                                cursorWidth,
                                Battlefield2042MoneyCursorWidth,
                                settleProgress);
                            DrawBattlefield2042GlowingRectangle(
                                drawingSession,
                                new Rect(
                                    cursorCenterX - cursorWidth / 2.0,
                                    centerY - Battlefield2042FeedCursorHalfHeight,
                                    cursorWidth,
                                    Battlefield2042FeedCursorHalfHeight * 2.0),
                                Battlefield2042EnemyColor,
                                cursorAlpha,
                                0.88);
                        }

                        if (exitProgress > 0 && exitProgress < 0.72)
                        {
                            byte glitchAlpha = (byte)Math.Max(
                                0,
                                Math.Min(255, (1.0 - (exitProgress / 0.72)) * 210));
                            DrawBattlefield2042GlitchBars(
                                drawingSession,
                                Battlefield2042FeedGlitchBarsB,
                                rightX + Lerp(-24, 44, exitEase),
                                centerY,
                                Color.FromArgb(
                                    glitchAlpha,
                                    Battlefield2042EnemyColor.R,
                                    Battlefield2042EnemyColor.G,
                                    Battlefield2042EnemyColor.B),
                                0);
                        }
                    }
                }

                row++;
            }
        }

        private void DrawBattlefield2042MoneyFeed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            int row = 0;
            for (int i = _battlefield2042HudState.MoneyItems.Count - 1; i >= 0; i--)
            {
                Battlefield2042MoneyItem item = _battlefield2042HudState.MoneyItems[i];
                double elapsed = now - item.RevealTimeMs;
                if (elapsed < 0)
                {
                    continue;
                }

                int visualRow = Math.Min(row, Battlefield2042MaxFeedLines - 1);
                double exitProgress = ResolveBattlefield2042ExitProgress(item.ExitStartTimeMs, now);
                double exitEase = EaseOutCubic(exitProgress);
                const double textScale = 1.02;
                string text = "+" + FormatBattlefieldMoney(item.MoneyReward);
                Rect textBounds = MeasureBattlefieldTextBounds(text, textFormat);
                double textWidth = textBounds.Width * textScale;
                double x = ResolveBattlefield2042MoneyFeedX(textWidth, exitEase);
                double centerY = Battlefield2042FeedBaseY
                    + visualRow * Battlefield2042FeedLineSpacing
                    + Battlefield2042FeedObjectHeight / 2.0
                    + (7 * exitEase);
                double originX = x - (textBounds.X * textScale);
                double originY = centerY
                    - ((textBounds.Y + (textBounds.Height / 2.0)) * textScale);
                double cursorStopX = x + textWidth + Battlefield2042MoneyCursorGap;
                double rootAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                    Battlefield2042FeedRootAlphaCurve,
                    elapsed)) * (1.0 - exitProgress);

                if (rootAlpha > 0.0001)
                {
                    Rect clip = LimitBattlefield2042FeedClip(
                        CreateBattlefield2042FeedClipRect(x, centerY, true, elapsed),
                        x - 10,
                        cursorStopX + Battlefield2042MoneyCursorWidth + 6);
                    using (drawingSession.CreateLayer((float)rootAlpha, clip))
                    {
                        double backgroundAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                            Battlefield2042FeedBackgroundAlphaCurve,
                            elapsed));
                        DrawBattlefield2042GlowingRectangle(
                            drawingSession,
                            new Rect(x - 4, centerY - 6, textWidth + 8, 12),
                            Colors.White,
                            backgroundAlpha,
                            0.52);

                        double textAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                            Battlefield2042FeedTextAlphaCurve,
                            elapsed));
                        byte alpha = (byte)Math.Max(0, Math.Min(255, textAlpha * 255));
                        DrawBattlefield2042GlowingTextAtLayoutOrigin(
                            drawingSession,
                            text,
                            originX,
                            originY,
                            textScale,
                            Color.FromArgb(alpha, 245, 249, 249),
                            0.78,
                            textFormat);
                        if (elapsed <= Battlefield2042FeedEffectDurationMs)
                        {
                            double cursorAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042MoneyCursorAlphaCurve,
                                elapsed));
                            cursorAlpha *= EaseOutCubic(Clamp01(
                                (elapsed - 716.6667) / 150.0));
                            double cursorHeight = Math.Max(0, EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042MoneyCursorHeightCurve,
                                elapsed));
                            DrawBattlefield2042GlowingRectangle(
                                drawingSession,
                                new Rect(
                                    cursorStopX,
                                    centerY - cursorHeight / 2.0,
                                    Battlefield2042MoneyCursorWidth,
                                    cursorHeight),
                                Battlefield2042EnemyColor,
                                cursorAlpha,
                                0.78);
                        }

                        if (exitProgress > 0 && exitProgress < 0.72)
                        {
                            byte glitchAlpha = (byte)Math.Max(
                                0,
                                Math.Min(255, (1.0 - (exitProgress / 0.72)) * 185));
                            DrawBattlefield2042GlitchBars(
                                drawingSession,
                                Battlefield2042FeedGlitchBarsA,
                                x + Lerp(18, -30, exitEase),
                                centerY,
                                Color.FromArgb(glitchAlpha, 245, 249, 249),
                                0);
                        }
                    }
                }

                row++;
            }
        }

        private void DrawBattlefield2042MoneyTotal(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            if (!IsBattlefield5MoneyVisible(now))
            {
                return;
            }

            double alpha = ResolveBattlefield5MoneyAlpha(now);
            double scale = ResolveBattlefield5MoneyScale(now, true) * 0.74;
            string text = FormatBattlefieldMoney(
                (int)Math.Round(ResolveBattlefield5MoneyValue(now)));
            double width = MeasureBattlefieldTextWidth(text, textFormat) * scale;
            DrawBattlefield2042Text(
                drawingSession,
                text,
                Battlefield2042FrameWidth / 2.0 + 155 - width,
                Battlefield2042MoneyTotalY,
                scale,
                Color.FromArgb(
                    (byte)Math.Max(0, Math.Min(255, alpha * 255)),
                    245,
                    249,
                    249),
                textFormat);
        }

        private static double ResolveBattlefield2042MoneyFeedX(
            double textWidth,
            double exitEase)
        {
            double defaultX = Battlefield2042FrameWidth / 2.0
                + Battlefield2042MoneyFeedLeftOffset;
            double rowRightLimit = Battlefield2042FrameWidth / 2.0
                + Battlefield2042FeedRowRightOffset;
            double rightConstrainedX = rowRightLimit
                - Battlefield2042MoneyCursorWidth
                - Battlefield2042MoneyCursorGap
                - Math.Max(0, textWidth);
            return Math.Min(defaultX, rightConstrainedX) - (36 * exitEase);
        }

        private static Rect LimitBattlefield2042FeedClip(
            Rect legacyClip,
            double contentLeft,
            double contentRight)
        {
            double left = Math.Max(legacyClip.X, contentLeft);
            double right = Math.Min(legacyClip.X + legacyClip.Width, contentRight);
            return new Rect(
                left,
                legacyClip.Y,
                Math.Max(0, right - left),
                legacyClip.Height);
        }

        private static double ResolveBattlefield2042ExitProgress(
            double exitStartTimeMs,
            double now)
        {
            if (exitStartTimeMs < 0 || now < exitStartTimeMs)
            {
                return 0;
            }

            return Clamp01((now - exitStartTimeMs) / Battlefield2042FeedExitDurationMs);
        }
        private static Rect CreateBattlefield2042FeedClipRect(
            double anchorX,
            double centerY,
            bool anchoredLeft,
            double elapsed)
        {
            double paddingX = EvaluateBattlefield2042LegacyCurve(
                Battlefield2042FeedMaskPaddingXCurve,
                elapsed);
            double paddingY = EvaluateBattlefield2042LegacyCurve(
                Battlefield2042FeedMaskPaddingYCurve,
                elapsed);
            double width = Math.Max(0, Battlefield2042FeedObjectWidth - paddingX * 2.0);
            double height = Math.Max(0, Battlefield2042FeedObjectHeight - paddingY * 2.0);
            double centerX = anchoredLeft
                ? anchorX + Battlefield2042FeedObjectWidth / 2.0
                : anchorX - Battlefield2042FeedObjectWidth / 2.0;
            return new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
        }

        private static double EvaluateBattlefield2042LegacyCurve(
            Battlefield2042LegacyCurveKey[] keys,
            double elapsedMs)
        {
            if (keys == null || keys.Length == 0)
            {
                return 0;
            }

            if (elapsedMs <= keys[0].TimeMs)
            {
                return keys[0].Value;
            }

            for (int i = 0; i < keys.Length - 1; i++)
            {
                Battlefield2042LegacyCurveKey current = keys[i];
                Battlefield2042LegacyCurveKey next = keys[i + 1];
                if (elapsedMs > next.TimeMs)
                {
                    continue;
                }

                double durationMs = next.TimeMs - current.TimeMs;
                if (durationMs <= 0)
                {
                    return next.Value;
                }

                double t = Clamp01((elapsedMs - current.TimeMs) / durationMs);
                double t2 = t * t;
                double t3 = t2 * t;
                double durationSeconds = durationMs / 1000.0;
                double m0 = current.OutSlope * durationSeconds;
                double m1 = next.InSlope * durationSeconds;
                return (2 * t3 - 3 * t2 + 1) * current.Value
                    + (t3 - 2 * t2 + t) * m0
                    + (-2 * t3 + 3 * t2) * next.Value
                    + (t3 - t2) * m1;
            }

            return keys[keys.Length - 1].Value;
        }

        private static void DrawBattlefield2042FeedGlitches(
            CanvasDrawingSession drawingSession,
            double elapsed,
            double originX,
            double originY)
        {
            if (elapsed >= 433.3333 && elapsed < 633.3333)
            {
                double x = elapsed >= 616.6667
                    ? 107.87
                    : elapsed >= 566.6667
                        ? -46.1
                        : elapsed >= 500
                            ? 15.3
                            : 0;
                double y = elapsed >= 566.6667
                    ? -1.3
                    : elapsed >= 500
                        ? 6
                        : 0;
                DrawBattlefield2042GlitchBars(
                    drawingSession,
                    Battlefield2042FeedGlitchBarsA,
                    originX + x,
                    originY - y,
                    Colors.White,
                    0);
            }

            if (elapsed >= 533.3333 && elapsed < 600)
            {
                double x = elapsed >= 583.3333 ? 29.1 : 0;
                double y = elapsed >= 583.3333 ? 3.21 : 0;
                DrawBattlefield2042GlitchBars(
                    drawingSession,
                    Battlefield2042FeedGlitchBarsB,
                    originX + x,
                    originY - y,
                    Colors.White,
                    0);
            }
        }

        private static void DrawBattlefield2042GlowingTextAtLayoutOrigin(
            CanvasDrawingSession drawingSession,
            string text,
            double originX,
            double originY,
            double scale,
            Color color,
            double glowStrength,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            glowStrength = Clamp01(glowStrength);
            byte glowAlpha = (byte)Math.Max(
                0,
                Math.Min(255, Math.Round(color.A * (0.34 + glowStrength * 0.34))));
            using (CanvasCommandList glowSource = new CanvasCommandList(drawingSession))
            {
                using (CanvasDrawingSession glowSession = glowSource.CreateDrawingSession())
                {
                    glowSession.Transform =
                        Matrix3x2.CreateScale((float)scale)
                        * Matrix3x2.CreateTranslation(
                            (float)Math.Round(originX),
                            (float)Math.Round(originY));
                    using (CanvasSolidColorBrush glowBrush = new CanvasSolidColorBrush(
                        glowSession,
                        Color.FromArgb(glowAlpha, color.R, color.G, color.B)))
                    {
                        glowSession.DrawText(text, 0, 0, glowBrush, format);
                    }
                }

                DrawBattlefield2042BlurredSource(
                    drawingSession,
                    glowSource,
                    (float)(3.2 + glowStrength * 1.4));
                DrawBattlefield2042BlurredSource(
                    drawingSession,
                    glowSource,
                    (float)(0.9 + glowStrength * 0.75));
            }

            DrawBattlefieldTextAtLayoutOrigin(
                drawingSession,
                text,
                originX,
                originY,
                scale,
                color,
                format);
        }

        private static void DrawBattlefield2042GlowingRectangle(
            CanvasDrawingSession drawingSession,
            Rect rect,
            Color color,
            double opacity,
            double bloomStrength)
        {
            opacity = Clamp01(opacity);
            bloomStrength = Clamp01(bloomStrength);
            if (opacity <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            byte glowAlpha = (byte)Math.Max(
                0,
                Math.Min(
                    255,
                    Math.Round(opacity * (0.34 + bloomStrength * 0.26) * 255)));
            using (CanvasCommandList glowSource = new CanvasCommandList(drawingSession))
            {
                using (CanvasDrawingSession glowSession = glowSource.CreateDrawingSession())
                {
                    glowSession.FillRectangle(
                        rect,
                        Color.FromArgb(glowAlpha, color.R, color.G, color.B));
                }

                DrawBattlefield2042BlurredSource(
                    drawingSession,
                    glowSource,
                    (float)(4.8 + bloomStrength * 2.4));
                DrawBattlefield2042BlurredSource(
                    drawingSession,
                    glowSource,
                    (float)(1.25 + bloomStrength * 1.3));
            }

            byte coreAlpha = (byte)Math.Max(
                0,
                Math.Min(
                    255,
                    Math.Round(opacity * (0.12 + bloomStrength * 0.08) * 255)));
            drawingSession.FillRectangle(
                rect,
                Color.FromArgb(coreAlpha, color.R, color.G, color.B));
        }

        private static void DrawBattlefield2042BlurredSource(
            CanvasDrawingSession drawingSession,
            CanvasCommandList source,
            float blurAmount)
        {
            using (GaussianBlurEffect blur = new GaussianBlurEffect
            {
                Source = source,
                BlurAmount = Math.Max(0.01f, blurAmount),
                Optimization = EffectOptimization.Speed,
                BorderMode = EffectBorderMode.Soft
            })
            {
                CanvasBlend previousBlend = drawingSession.Blend;
                try
                {
                    drawingSession.Blend = CanvasBlend.Add;
                    drawingSession.DrawImage(blur);
                }
                finally
                {
                    drawingSession.Blend = previousBlend;
                }
            }
        }

        private void DrawBattlefield2042KilllogExitGlitch(
            CanvasDrawingSession drawingSession,
            double now)
        {
            if (_battlefield2042HudState.LastKillLogTriggerTimeMs < 0)
            {
                return;
            }

            double elapsed = now - _battlefield2042HudState.LastKillLogTriggerTimeMs;
            if (elapsed < 3100 || elapsed >= Battlefield2042KillLogDurationMs)
            {
                return;
            }

            Battlefield2042GlitchBar[] bars = elapsed < 3133.3333
                ? Battlefield2042KilllogGlitchBarsA
                : Battlefield2042KilllogGlitchBarsB;
            DrawBattlefield2042GlitchBars(
                drawingSession,
                bars,
                Battlefield2042FrameWidth / 2.0,
                150,
                Battlefield2042KilllogGlitchColor,
                0);
        }
        private static void DrawBattlefield2042Text(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Color shadow = Color.FromArgb((byte)(color.A * 0.72), 0, 0, 0);
            DrawBattlefieldText(drawingSession, text, x + 1, y + 1, scale, shadow, format);
            DrawBattlefieldText(drawingSession, text, x, y, scale, color, format);
        }

        private static void DrawBattlefield2042Image(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            drawingSession.DrawImage(
                image,
                target,
                new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height),
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);
        }

        private void ResetBattlefield2042HudState()
        {
            _isBattlefield2042HudActive = false;
            _battlefield2042HudState.Clear();
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

            public void StartExit(double startTimeMs)
            {
                if (!IsExiting)
                {
                    ExitStartTimeMs = startTimeMs;
                }
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

            public void StartExit(double startTimeMs)
            {
                if (!IsExiting)
                {
                    ExitStartTimeMs = startTimeMs;
                }
            }
        }
    }
}
