using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
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

    }
}
