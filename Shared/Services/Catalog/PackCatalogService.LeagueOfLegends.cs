// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    public static partial class PackCatalogService
    {
        public static readonly IReadOnlyList<string> LeagueOfLegendsIconSlotFileNames = new[]
        {
            "AnnouncementText_GoldGradientFill.png",
            "Ripple_NoShadow.png",
            "AnnBannerBlue_Underline.png",
            "AnnBannerRed_Underline.png",
            "AnnBannerGold_Underline.png",
            "AnnBannerTopIcon_gold_ring.png",
            "AnnBannerTopIcon_glow_blue.png",
            "AnnBannerTopIcon_glow_red.png",
            "AnnBannerLeftIcon_gold_ring.png",
            "AnnBannerLeftGlowBlue.png",
            "AnnBannerLeftGlowRed.png",
            "AnnBannerRightIcon_gold_ring.png",
            "RightIconGlowBlue.png",
            "RightIconGlowRed.png",
            "DoubleKillBlueBG.png",
            "DoubleKillRedBG.png",
            "TripleKillBlueBG.png",
            "TripleKillRedBG.png",
            "QuadraKillBlueBG.png",
            "QuadraKillRedBG.png",
            "PentakillBlueBG.png",
            "PentakillRedBG.png",
            "KillCallout0_Background.png",
            "KillCallout0_KillerFrame.png",
            "KillCallout0_TargetFrame.png",
            "KillCallout0_KillIconNormal.png",
            "KillCallout0_KillIconStreak.png",
            "KillCallout0_KillIconShutdown.png",
            "KillCallout0_KillIconBounty.png",
            "KillCallout0_CaptureAltar.png",
            "KillCallout0_ReviveAllyIcon.png"
        };

        public static bool IsLeagueOfLegendsIconPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (key.StartsWith("custom_leagueoflegends_icon_", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "leagueoflegends_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || GameStyleService.IsLeagueOfLegendsKey(key));
        }

        public static Task ImportLeagueOfLegendsIconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_leagueoflegends_icon_", folder);

        public static Task CreateLeagueOfLegendsIconPackAsync(
            string displayName,
            IReadOnlyDictionary<string, StorageFile> selectedFiles,
            StorageFile headImageFile = null)
            => CreateGameIconPackAsync("leagueoflegends", "custom_leagueoflegends_icon_", displayName, selectedFiles, headImageFile);

        public static async Task<string> ImportLeagueOfLegendsIconManifestPackAsync(StorageFolder source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            StorageFile manifestFile = await source.GetFileAsync("manifest.json");
            JsonObject manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
            string declaredId = manifest.GetNamedString("id", "");
            string key = !string.IsNullOrWhiteSpace(declaredId)
                && declaredId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && (string.Equals(declaredId, "leagueoflegends_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("leagueoflegends_icon_", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("custom_leagueoflegends_icon_", StringComparison.OrdinalIgnoreCase))
                ? declaredId
                : "custom_leagueoflegends_icon_" + Guid.NewGuid().ToString("N");

            string displayName = manifest.GetNamedString("display_name_zh_cn",
                manifest.GetNamedString("display_name", manifest.GetNamedString("name", source.DisplayName)));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "英雄联盟 图标包";
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("leagueoflegends");
            StorageFolder staged = await root.CreateFolderAsync(".import-leagueoflegends-" + Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
            bool committed = false;
            try
            {
                await CopyAudioPackFolderAsync(source, staged);
                await staged.RenameAsync(SanitizeName(displayName), NameCollisionOption.GenerateUniqueName);

                var catalog = await LoadAsync();
                var previous = catalog.IconPacks.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
                var item = new IconPackItem
                {
                    Key = key,
                    DisplayName = displayName,
                    FolderPath = staged.Path,
                    IsBuiltIn = false,
                    IsVisibleInWidget = previous != null ? previous.IsVisibleInWidget : true,
                    OwnsFolder = true
                };

                var updated = catalog.IconPacks
                    .Select(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase) ? item : p)
                    .ToList();
                if (previous == null)
                {
                    updated.Add(item);
                }
                catalog.IconPacks = updated;
                await SaveAsync(catalog);
                committed = true;

                if (previous != null && previous.OwnsFolder
                    && !string.IsNullOrWhiteSpace(previous.FolderPath)
                    && !string.Equals(previous.FolderPath, staged.Path, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (Directory.Exists(previous.FolderPath))
                        {
                            Directory.Delete(previous.FolderPath, true);
                        }
                    }
                    catch
                    {
                    }
                }

                return displayName;
            }
            finally
            {
                if (!committed)
                {
                    try { await staged.DeleteAsync(StorageDeleteOption.PermanentDelete); } catch { }
                }
            }
        }
    }
}
