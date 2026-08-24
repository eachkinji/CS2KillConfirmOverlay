using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlayCsolKill(int killCount, string specialIconKey)
        {
            PlayInternal(progress => LoadCsolKillAssetAsync(killCount, specialIconKey, progress));
        }

        private async Task PreloadCsolAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            await LoadCsolKillAssetAsync(1, null, progress);
            progress?.Report(100);
        }

        private static string GetCsolSpecialFileName(string specialKey)
        {
            switch ((specialKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "headshot":
                    return "headshot_kill.png";
                case "melee":
                    return "melee_kill.png";
                case "revenge":
                    return "revenge.png";
                case "firstkill":
                    return "firstkill.png";
                case "assist":
                    return "assist.png";
                default:
                    return null;
            }
        }

    }
}
