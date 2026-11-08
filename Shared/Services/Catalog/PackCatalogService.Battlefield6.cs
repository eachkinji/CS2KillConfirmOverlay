// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    public static partial class PackCatalogService
    {
        public static readonly IReadOnlyList<string> Battlefield6IconSlotFileNames = new[]
        {
            "killicon_battlefield6_default.png",
            "killicon_battlefield6_headshot.png",
            "killicon_battlefield6_explosion.png",
            "killicon_battlefield6_crit.png",
            "killicon_battlefield6_destroyvehicle.png",
            "killicon_battlefield6_assist.png",
            "killicon_battlefield6_capture.png",
            "killicon_battlefield6_vehicle_destroy_assist.png",
            "killicon_battlefield6_medic.png",
            "killicon_battlefield6_spot_assist.png",
        };

        public static bool IsBattlefield6IconPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (key.StartsWith("custom_battlefield6_icon_", StringComparison.OrdinalIgnoreCase)
                    || GameStyleService.IsBattlefield6Key(key));
        }

        public static Task ImportBattlefield6IconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_battlefield6_icon_", folder);

        public static Task CreateBattlefield6IconPackAsync(string displayName,
            IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("battlefield6", "custom_battlefield6_icon_", displayName, selectedFiles, headImageFile);
    }
}
