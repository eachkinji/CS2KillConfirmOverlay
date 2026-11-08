// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if WINDOWS_UWP
using KillConfirmGameBar.Services;
#else
using KillConfirmCompatibility.Services;
#endif
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlayCsolKill(int killCount, string specialIconKey)
        {
            PlayInternal(progress => LoadCsolKillAssetAsync(killCount, specialIconKey, progress, requireCached: true));
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
                case "grenade_kill":
                case "grenade":
                    return "grenade_kill.png";
                default:
                    return null;
            }
        }

    }
}
