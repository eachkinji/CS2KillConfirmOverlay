using KillConfirmCompatibility.Desktop.Runtime;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmCompatibility.Services
{
    public static partial class PackCatalogService
    {
        internal static async Task AddDesktopPackAsync(StorageFolder folder, GameStyleMode game, bool voice, string displayName)
        {
            var catalog = await LoadAsync();
            string gameKey = GameStyleService.ToStorageValue(game);
            string prefix = game == GameStyleMode.Crossfire ? "custom_" : "custom_" + gameKey + "_";
            string key = prefix + (voice ? "voice_" : "icon_") + Guid.NewGuid().ToString("N");
            if (voice)
                catalog.VoicePacks.Add(new VoicePackItem { Key = key, DisplayName = displayName, FolderPath = folder.Path, IsVisibleInWidget = true, OwnsFolder = true });
            else
            {
                var capabilities = await DetectIconPackCapabilitiesAsync(folder);
                catalog.IconPacks.Add(new IconPackItem { Key = key, DisplayName = displayName, FolderPath = folder.Path, IsVisibleInWidget = true, OwnsFolder = true,
                    HasFxOverlay = capabilities.HasKillFxOverlay, HasKillFxOverlay = capabilities.HasKillFxOverlay, HasEliteOverlay = capabilities.HasEliteOverlay, HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay });
            }
            await SaveAsync(catalog);
            DesktopStorage.Current.LocalSettings.Values[(voice ? "VoicePack." : "KillIconPack.") + gameKey] = key;
        }
    }
}
