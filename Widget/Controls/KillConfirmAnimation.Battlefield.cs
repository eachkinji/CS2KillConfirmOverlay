using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double BattlefieldFrameWidth = 607;
        private const double BattlefieldFrameHeight = 260;
        private const int BattlefieldTextLineHeight = 10;
        private const string BattlefieldFontFamily = "Segoe UI";
        private static readonly Dictionary<string, CanvasBitmap> BattlefieldIconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);

        private async Task<AnimationAsset> LoadBattlefieldKillAssetAsync(
            string styleKey,
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponLabel,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch,
            IProgress<int> progress = null)
        {
            string normalizedStyle = string.Equals(styleKey, "bf5", StringComparison.OrdinalIgnoreCase) ? "bf5" : "bf1";
            string iconFileName = GetBattlefieldIconFileName(normalizedStyle, isHeadshot, isAssist, isKnifeKill);
            progress?.Report(35);

            bool isTextOnly = IsBattlefieldTextOnlyEvent(isAssist, eventKind);
            CanvasBitmap icon = isTextOnly ? null : await LoadBattlefieldIconAsync(normalizedStyle, iconFileName);

            progress?.Report(100);
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)BattlefieldFrameWidth,
                    FrameHeight = (int)BattlefieldFrameHeight,
                    Frames = string.Equals(normalizedStyle, "bf5", StringComparison.OrdinalIgnoreCase)
                        ? Battlefield5FrameCount
                        : Battlefield1FrameCount,
                    Fps = FrameSequenceFps
                },
                new BattlefieldKillAsset
                {
                    StyleKey = normalizedStyle,
                    KillCount = Math.Max(1, killCount),
                    IsHeadshot = isHeadshot,
                    IsAssist = isAssist,
                    IsCrit = isKnifeKill,
                    IsTextOnly = isTextOnly,
                    EventKind = NormalizeBattlefieldEventKind(isAssist, eventKind),
                    RoundNumber = Math.Max(0, roundNumber),
                    MoneyEpoch = Math.Max(0, moneyEpoch),
                    PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Unknown" : playerName.Trim(),
                    WeaponLabel = ResolveBattlefieldWeaponName(weaponLabel),
                    HealthText = isAssist ? "0" : Math.Max(1, killCount).ToString(),
                    MoneyReward = Math.Max(0, moneyReward),
                    Icon = icon
                });
        }

        private async Task PreloadBattlefieldAnimationsAsync(string styleKey, IProgress<int> progress)
        {
            string normalizedStyle = string.Equals(styleKey, "bf5", StringComparison.OrdinalIgnoreCase) ? "bf5" : "bf1";
            string[] iconFiles = string.Equals(normalizedStyle, "bf5", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    "killicon_battlefield5_default.png",
                    "killicon_battlefield5_headshot.png"
                }
                : new[]
                {
                    "killicon_battlefield1_default.png",
                    "killicon_battlefield1_headshot.png",
                    "killicon_battlefield1_crit.png",
                    "killicon_battlefield1_explosion.png"
                };

            progress?.Report(0);
            for (int i = 0; i < iconFiles.Length; i++)
            {
                try
                {
                    await LoadBattlefieldIconAsync(normalizedStyle, iconFiles[i]);
                }
                catch
                {
                }

                int percent = (int)Math.Round((i + 1) * 100.0 / iconFiles.Length);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
        }

        private static async Task<CanvasBitmap> LoadBattlefieldIconAsync(string styleKey, string iconFileName)
        {
            string normalizedStyle = string.Equals(styleKey, "bf5", StringComparison.OrdinalIgnoreCase) ? "bf5" : "bf1";
            string cacheKey = normalizedStyle + "/" + iconFileName;
            lock (BattlefieldIconCache)
            {
                if (BattlefieldIconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = await LoadBitmapFromApplicationUriAsync(
                $"ms-appx:///Assets/GameStyles/{GetBattlefieldAssetFolder(normalizedStyle)}/killconfirm/textures/{iconFileName}");

            lock (BattlefieldIconCache)
            {
                if (BattlefieldIconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }

                BattlefieldIconCache[cacheKey] = loaded;
                return loaded;
            }
        }

        private static void ClearBattlefieldIconCache()
        {
            BattlefieldIconCache.Clear();
        }

        private static string NormalizeBattlefieldEventKind(bool isAssist, string eventKind)
        {
            if (isAssist)
            {
                return "assist";
            }

            string normalized = string.IsNullOrWhiteSpace(eventKind)
                ? string.Empty
                : eventKind.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "assist":
                case "round_win":
                case "round_loss":
                case "bomb_plant":
                case "bomb_defuse":
                case "hostage_interact":
                case "hostage_rescue":
                case "kill":
                    return normalized;
                default:
                    return "kill";
            }
        }

        private static bool IsBattlefieldTextOnlyEvent(bool isAssist, string eventKind)
        {
            string normalized = NormalizeBattlefieldEventKind(isAssist, eventKind);
            return string.Equals(normalized, "assist", StringComparison.OrdinalIgnoreCase)
                || IsRoundBonusEvent(normalized)
                || IsObjectiveBonusEvent(normalized);
        }

        private static bool IsRoundBonusEvent(string eventKind)
        {
            return IsRoundWinEvent(eventKind) || IsRoundLossEvent(eventKind);
        }

        private static bool IsRoundWinEvent(string eventKind)
        {
            return string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRoundLossEvent(string eventKind)
        {
            return string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsObjectiveBonusEvent(string eventKind)
        {
            return string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetObjectiveBonusLabel(string eventKind)
        {
            switch (eventKind)
            {
                case "bomb_plant":
                    return "\u5b89\u653e\u70b8\u5f39";
                case "bomb_defuse":
                    return "\u62c6\u9664\u70b8\u5f39";
                case "hostage_interact":
                    return "\u63a5\u89e6\u4eba\u8d28";
                case "hostage_rescue":
                    return "\u6551\u51fa\u4eba\u8d28";
                default:
                    return "\u76ee\u6807\u5956\u52b1";
            }
        }

        private static string GetObjectiveBonusLabelEnglish(string eventKind)
        {
            switch (eventKind)
            {
                case "bomb_plant":
                    return "BOMB PLANTED";
                case "bomb_defuse":
                    return "BOMB DEFUSED";
                case "hostage_interact":
                    return "HOSTAGE SECURED";
                case "hostage_rescue":
                    return "HOSTAGE RESCUED";
                default:
                    return "OBJECTIVE BONUS";
            }
        }

        private void DrawBattlefieldKillFrame(CanvasDrawingSession drawingSession, int frame)
        {
            BattlefieldKillAsset asset = _currentBattlefieldAsset;
            if (asset == null)
            {
                return;
            }

            if (string.Equals(asset.StyleKey, "bf5", StringComparison.OrdinalIgnoreCase))
            {
                DrawBattlefield5SingleFrame(drawingSession, asset, frame);
                return;
            }

            if (asset.IsTextOnly)
            {
                DrawBattlefield1TextOnlyFrame(drawingSession, asset, frame);
                return;
            }

            DrawBattlefield1Frame(drawingSession, asset, frame);
        }

        private static double ResolveBattlefieldAlpha(double elapsedSeconds, double animationSeconds, double displaySeconds)
        {
            if (elapsedSeconds < animationSeconds)
            {
                return Clamp01(elapsedSeconds / animationSeconds);
            }

            if (elapsedSeconds > displaySeconds)
            {
                double fadeElapsed = elapsedSeconds - displaySeconds;
                if (fadeElapsed >= animationSeconds)
                {
                    return 0;
                }

                return Clamp01(1.0 - (fadeElapsed / animationSeconds));
            }

            return 1.0;
        }

        private static string GetBattlefieldAssetFolder(string style)
        {
            return string.Equals(style, "bf5", StringComparison.OrdinalIgnoreCase)
                ? "battlefield5"
                : "battlefield1";
        }

        private static string GetBattlefieldIconFileName(string styleKey, bool isHeadshot, bool isAssist, bool isCrit)
        {
            if (string.Equals(styleKey, "bf5", StringComparison.OrdinalIgnoreCase))
            {
                if (isHeadshot)
                {
                    return "killicon_battlefield5_headshot.png";
                }

                if (isAssist)
                {
                    return "killicon_battlefield5_assist.png";
                }

                return "killicon_battlefield5_default.png";
            }

            if (isHeadshot)
            {
                return "killicon_battlefield1_headshot.png";
            }

            return isCrit
                ? "killicon_battlefield1_crit.png"
                : "killicon_battlefield1_default.png";
        }

        private static int ResolveBattlefieldKillType(bool isHeadshot, bool isCrit, bool isAssist)
        {
            if (isHeadshot)
            {
                return BattlefieldKillTypeHeadshot;
            }

            if (isAssist)
            {
                return BattlefieldKillTypeAssist;
            }

            if (isCrit)
            {
                return BattlefieldKillTypeCrit;
            }

            return BattlefieldKillTypeNormal;
        }

        private static string ResolveBattlefieldWeaponName(string weaponLabel)
        {
            if (string.IsNullOrWhiteSpace(weaponLabel))
            {
                return "Unknown";
            }

            switch (weaponLabel.Trim().ToLowerInvariant())
            {
                case "assault":
                    return "Assault";
                case "elite":
                    return "Machine Gun";
                case "scout":
                    return "SMG";
                case "sniper":
                    return "Sniper";
                case "knife":
                    return "Knife";
                default:
                    return weaponLabel.Trim();
            }
        }

        private static CanvasTextFormat CreateBattlefieldTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = BattlefieldFontFamily,
                FontSize = BattlefieldTextLineHeight,
                FontWeight = FontWeights.Bold
            };
        }

        private static double MeasureBattlefieldTextWidth(string text, CanvasTextFormat format)
        {
            Rect bounds = MeasureBattlefieldTextBounds(text, format);
            return Math.Max(0, Math.Ceiling(bounds.Width));
        }

        private static Rect MeasureBattlefieldTextBounds(string text, CanvasTextFormat format)
        {
            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                string.IsNullOrEmpty(text) ? " " : text,
                format,
                1000,
                100))
            {
                return layout.DrawBounds;
            }
        }

        private static double MeasureBattlefieldTextAdvance(string text, CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                format,
                1000,
                100))
            {
                return Math.Max(0, layout.LayoutBounds.Width);
            }
        }

        private static void DrawBattlefieldTextAtLayoutOrigin(
            CanvasDrawingSession drawingSession,
            string text,
            double originX,
            double originY,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            Matrix3x2 previousTransform = drawingSession.Transform;
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)scale)
                * Matrix3x2.CreateTranslation(
                    (float)Math.Round(originX),
                    (float)Math.Round(originY))
                * previousTransform;

            try
            {
                float shadowOffset = (float)(1.0 / scale);
                using (CanvasSolidColorBrush shadowBrush = new CanvasSolidColorBrush(
                    drawingSession,
                    Color.FromArgb((byte)Math.Max(0, color.A * 0.65), 0, 0, 0)))
                using (CanvasSolidColorBrush textBrush = new CanvasSolidColorBrush(
                    drawingSession,
                    color))
                {
                    drawingSession.DrawText(
                        text,
                        shadowOffset,
                        shadowOffset,
                        shadowBrush,
                        format);
                    drawingSession.DrawText(text, 0, 0, textBrush, format);
                }
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }
        private static void DrawBattlefieldText(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            Rect bounds = MeasureBattlefieldTextBounds(text, format);
            double snappedX = Math.Round(x - (bounds.X * scale));
            double snappedY = Math.Round(y - (bounds.Y * scale));
            Matrix3x2 previousTransform = drawingSession.Transform;
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)scale)
                * Matrix3x2.CreateTranslation((float)snappedX, (float)snappedY)
                * previousTransform;

            try
            {
                const double shadowOffset = 0.0;
                using (CanvasSolidColorBrush shadowBrush = new CanvasSolidColorBrush(
                    drawingSession,
                    Color.FromArgb((byte)Math.Max(0, color.A * 0.65), 0, 0, 0)))
                using (CanvasSolidColorBrush textBrush = new CanvasSolidColorBrush(drawingSession, color))
                {
                    drawingSession.DrawText(text, (float)shadowOffset, (float)shadowOffset, shadowBrush, format);
                    drawingSession.DrawText(text, 0, 0, textBrush, format);
                }
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }

        private static void DrawBattlefieldTextCentered(
            CanvasDrawingSession drawingSession,
            string text,
            double centerX,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            double width = MeasureBattlefieldTextWidth(text, format) * scale;
            DrawBattlefieldText(drawingSession, text, centerX - (width / 2.0), y, scale, color, format);
        }

        private static void DrawBattlefieldTextRightAligned(
            CanvasDrawingSession drawingSession,
            string text,
            double rightX,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            double width = MeasureBattlefieldTextWidth(text, format) * scale;
            DrawBattlefieldText(drawingSession, text, rightX - width, y, scale, color, format);
        }


        private static void DrawBattlefieldImageStretch(CanvasDrawingSession drawingSession, CanvasBitmap image, Rect target, double opacity)
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
                CanvasImageInterpolation.NearestNeighbor);
        }

        private static double EaseOutCubic(double value)
        {
            double t = Clamp01(value);
            return 1.0 - Math.Pow(1.0 - t, 3);
        }

        private static double EaseOutQuint(double value)
        {
            double t = Clamp01(value);
            return 1.0 - Math.Pow(1.0 - t, 5);
        }

        private const int BattlefieldKillTypeNormal = 0;
        private const int BattlefieldKillTypeHeadshot = 1;
        private const int BattlefieldKillTypeCrit = 2;
        private const int BattlefieldKillTypeAssist = 3;
    }
}
