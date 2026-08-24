using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double PubgFrameWidth = 607;
        private const double PubgFrameHeight = 260;

        // gd656killicon official preset 00006: subtitle/kill_feed.
        private const double PubgFeedDisplayMs = 5000;
        private const double PubgFeedFadeInMs = 200;
        private const double PubgFeedFadeOutMs = 300;
        private const double PubgQueueIntervalMs = 200;
        private const double PubgFeedLineSpacing = 15;
        private const int PubgMaxFeedLines = 5;
        private const int PubgMaxPendingItems = 10;

        // gd656killicon official preset 00006: subtitle/combo.
        private const double PubgComboDisplayMs = 5000;
        private const double PubgComboFadeInMs = 200;
        private const double PubgComboExitMs = 500;
        private const double PubgComboScale = 1.5;
        private const double PubgLightScanMs = 400;
        private const double PubgLightFadeMs = 200;
        private const double PubgLightScanDistance = 20;
        private const double PubgLightHeight = 10;

        private readonly PubgHudState _pubgHudState = new PubgHudState();
        private bool _isPubgHudActive;


        private sealed class PubgHudState
        {
            public readonly List<PubgFeedItem> FeedItems = new List<PubgFeedItem>();
            public readonly Queue<PubgFeedItem> PendingFeedItems = new Queue<PubgFeedItem>();
            public readonly Queue<PubgComboItem> PendingComboItems = new Queue<PubgComboItem>();
            public double LastFeedDequeueTimeMs = -PubgQueueIntervalMs;
            public double LastComboDequeueTimeMs = -PubgQueueIntervalMs;
            public bool ComboVisible;
            public int CurrentCombo;
            public bool ComboIsAssist;
            public double ComboStartTimeMs = -1;
            public int KillComboCount;
            public int AssistComboCount;
            public int RoundNumber = -1;
            public int MoneyEpoch = -1;

            public void ResetCombos()
            {
                PendingComboItems.Clear();
                LastComboDequeueTimeMs = -PubgQueueIntervalMs;
                ComboVisible = false;
                CurrentCombo = 0;
                ComboIsAssist = false;
                ComboStartTimeMs = -1;
                KillComboCount = 0;
                AssistComboCount = 0;
            }

            public void Clear()
            {
                FeedItems.Clear();
                PendingFeedItems.Clear();
                LastFeedDequeueTimeMs = -PubgQueueIntervalMs;
                RoundNumber = -1;
                MoneyEpoch = -1;
                ResetCombos();
            }
        }

        private enum PubgFeedKind
        {
            Plain,
            Normal,
            Headshot,
            Assist
        }

        private sealed class PubgFeedItem
        {
            public PubgFeedItem(
                PubgFeedKind kind,
                string plainText,
                string weaponName,
                string targetName)
            {
                Kind = kind;
                PlainText = plainText ?? string.Empty;
                WeaponName = weaponName ?? string.Empty;
                TargetName = targetName ?? string.Empty;
            }

            public static PubgFeedItem Plain(string text)
            {
                return new PubgFeedItem(PubgFeedKind.Plain, text, string.Empty, string.Empty);
            }

            public PubgFeedKind Kind { get; }
            public string PlainText { get; }
            public string WeaponName { get; }
            public string TargetName { get; }
            public double SpawnTimeMs { get; set; }
            public double CurrentY { get; set; }
        }

        private sealed class PubgComboItem
        {
            public PubgComboItem(int combo, bool isAssist)
            {
                Combo = combo;
                IsAssist = isAssist;
            }

            public int Combo { get; }
            public bool IsAssist { get; }
        }

        private sealed class PubgTextSegment
        {
            public PubgTextSegment(string text, Color color)
            {
                Text = text ?? string.Empty;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }
    }
}
