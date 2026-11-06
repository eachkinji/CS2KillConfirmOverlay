using System;
using Microsoft.Graphics.Canvas;
using KillConfirmCompatibility.Services;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlayBattlefield6Kill(int killCount, bool isHeadshot, bool isKnifeKill,
            bool isGrenadeKill, bool isAssist, string playerName, string weaponLabel,
            int moneyReward, string eventKind, int roundNumber, int moneyEpoch)
        {
            QueuePreparedPlayback("Battlefield6", () =>
            {
                PrepareBattlefield6HudPlayback();
                int session = _battlefield6Session;
                int token = _playToken;
                int generation = _resourceGeneration;
                string kind = NormalizeBattlefieldEventKind(isAssist, eventKind);
                bool round = IsRoundBonusEvent(kind);
                bool objective = IsObjectiveBonusEvent(kind);
                var item = new Battlefield6Event
                {
                    Headshot = isHeadshot && !isAssist && !objective && !round,
                    Rescue = kind == "hostage_rescue",
                    HasVictim = !objective && !round,
                    Victim = string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim(),
                    Label = ResolveBattlefield6Label(kind, isHeadshot, isKnifeKill, isGrenadeKill),
                    Reward = Math.Max(0, moneyReward), Round = Math.Max(0, roundNumber), Epoch = Math.Max(0, moneyEpoch)
                };
                _battlefield6HudState.Pending.Enqueue(item);
                while (_battlefield6HudState.Pending.Count > Battlefield6MaxPending)
                    _battlefield6HudState.Pending.Dequeue();

                try
                {
                    if (!round)
                    {
                        string suffix = item.Rescue ? "medic" : objective ? "capture" : isAssist ? "assist"
                            : isHeadshot ? "headshot" : isGrenadeKill ? "explosion" : "default";
                        CanvasBitmap bitmap = GetPreparedBattlefield6Icon("killicon_battlefield6_" + suffix + ".png");
                        if (!_isBattlefield6HudActive || session != _battlefield6Session
                            || token != _playToken || generation != _resourceGeneration) return;
                        item.Bitmap = bitmap;
                    }
                    item.Ready = true;
                    UpdateBattlefield6HudFrame();
                }
                catch (Exception ex)
                {
                    if (session != _battlefield6Session || token != _playToken
                        || generation != _resourceGeneration) return;
                    // A missing custom image must not leave the queue permanently waiting.
                    item.Ready = true;
                    App.Log("Battlefield 6 asset load failed: " + ex);
                    UpdateBattlefield6HudFrame();
                }
            });
        }

        private static string ResolveBattlefield6Label(string kind, bool headshot, bool knife, bool grenade)
        {
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (IsRoundBonusEvent(kind)) return IsRoundWinEvent(kind)
                ? (chinese ? "胜利奖励" : "Victory") : (chinese ? "失败奖励" : "Defeat");
            if (IsObjectiveBonusEvent(kind))
            {
                if (chinese) return GetObjectiveBonusLabel(kind);
                switch (kind)
                {
                    case "bomb_plant": return "Bomb planted";
                    case "bomb_defuse": return "Bomb defused";
                    case "hostage_interact": return "Hostage found";
                    default: return "Rescue";
                }
            }
            if (kind == "assist") return chinese ? "助攻" : "Assist";
            if (headshot) return chinese ? "精确击败" : "Headshot";
            if (grenade) return chinese ? "爆炸击杀" : "Grenade kill";
            if (knife) return chinese ? "近战击杀" : "Melee kill";
            return chinese ? "击杀" : "Kill";
        }

        private void PrepareBattlefield6HudPlayback()
        {
            if (_isBattlefield6HudActive) return;
            _battlefield6Session++;
            _battlefield6HudState.Clear();
            _customSequencePlaying = false;
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetApexFeedState();
            ResetModernWarfare2019State();
            ResetSanguoshaState();
            _isBattlefield6HudActive = true;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)Battlefield6FrameWidth, FrameHeight = (int)Battlefield6FrameHeight,
                Frames = 4 * FrameSequenceFps, Fps = FrameSequenceFps
            };
            EnsureBattlefield6TextFormats();
            ApplyViewportSize(Battlefield6FrameWidth, Battlefield6FrameHeight);
            HideLoadingProgress();
            Visibility = Windows.UI.Xaml.Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            if (!_timer.IsEnabled) _timer.Start();
        }

        private void ResetBattlefield6HudState()
        {
            _battlefield6Session++;
            _isBattlefield6HudActive = false;
            _battlefield6HudState.Clear();
            ClearBattlefield6TextLayouts();
            _battlefield6TextFormat?.Dispose();
            _battlefield6TextFormat = null;
            _battlefield6ScoreFormat?.Dispose();
            _battlefield6ScoreFormat = null;
            _battlefield6BonusFormat?.Dispose();
            _battlefield6BonusFormat = null;
        }
    }
}
