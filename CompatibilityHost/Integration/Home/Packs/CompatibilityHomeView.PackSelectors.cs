using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
    {
        private int _lastCrossfirePackRevision = -1;

        private async void OnPackCatalogChanged(object sender, EventArgs e)
        {
            if (!_isPageActive)
            {
                return;
            }

            try
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (_isPageActive)
                    {
                        if (GameStyleService.Current == GameStyleMode.Crossfire
                            && _lastCrossfirePackRevision != CrossfireExternalAssetService.Revision)
                        {
                            _lastCrossfirePackRevision = CrossfireExternalAssetService.Revision;
                        }
                        await InitializePackSelectorsAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                if (_isPageActive)
                {
                    App.LogCrash("Pack catalog dispatch failed: " + ex);
                }
            }
        }

        private async Task InitializePackSelectorsAsync()
        {
            await _packSelectorInitializationLock.WaitAsync();
            if (!_isPageActive) { _packSelectorInitializationLock.Release(); return; }
            GameStyleMode requestStyle = GameStyleService.Current;
            bool previousVoiceSuppression = _suppressVoicePackEvents;
            bool previousIconSuppression = _suppressIconPackEvents;
            _packSelectorsInitialized = false;
            _suppressVoicePackEvents = true;
            _suppressIconPackEvents = true;
            try
            {
                await PopulateVoicePackSelectorAsync();
                if (!_isPageActive || requestStyle != GameStyleService.Current) return;
                await PopulateIconPackSelectorAsync();
                if (!_isPageActive || requestStyle != GameStyleService.Current) return;
                LoadIconPackSetting();
                LoadEliteEffectSetting();
                LoadKillFxSetting();
                LoadWeaponBadgeSetting();
                LoadMainAnimationStyleSetting();
                LoadVoicePackSetting();
                if (GameStyleService.Current == GameStyleMode.Valorant
                    && ValorantPackSyncSettingsStore.Load())
                {
                    TrySyncValorantIconPackForVoiceSelection(GetSelectedVoicePackPreset());
                }
                ApplyGameStyleUi();
                _packSelectorsInitialized = true;
                if (_isPageActive)
                {
                    _ = WarmStartupAnimationCacheAsync(0);
                }
            }
            finally
            {
                _suppressVoicePackEvents = previousVoiceSuppression;
                _suppressIconPackEvents = previousIconSuppression;
                _packSelectorInitializationLock.Release();
            }
        }

        private async Task InitializePackSelectorsAndServiceAsync()
        {
            try
            {
                await InitializePackSelectorsAsync();
            }
            catch (Exception ex)
            {
                App.Log("Initialize pack selectors before service sync failed: " + ex);
            }

            if (_isPageActive)
            {
                try
                {
                    await EnsureServiceAvailableAsync();
                }
                catch (Exception ex)
                {
                    App.LogCrash("Initialize companion service failed: " + ex);
                }
                finally
                {
                    if (_isPageActive && !_shutdownRequested)
                    {
                        // Do not attach the event stream until the icon pack and the
                        // initial companion launch attempt have both completed. If
                        // launch failed, the stream's recovery path can retry it.
                        // The desktop host owns the event stream.
                    }
                }
            }
        }

        private async Task PopulateVoicePackSelectorAsync()
        {
            GameStyleMode style = GameStyleService.Current;
            string preferredPreset = LoadPackSettingForStyle(
                VoicePackSettingKey,
                style,
                GameStyleService.DefaultVoicePackKey(style));

            var visiblePacks = (await PackCatalogService.GetVisibleVoicePacksAsync()).ToList();
            if (!visiblePacks.Any(pack => string.Equals(pack.Key, preferredPreset, StringComparison.OrdinalIgnoreCase)))
            {
                VoicePackItem preferredPack = await PackCatalogService.GetVoicePackAsync(preferredPreset);
                if (preferredPack != null && GameStyleService.GetStyleForPackKey(preferredPack.Key) == style)
                {
                    visiblePacks.Insert(0, preferredPack);
                }
                else
                {
                    string fallback = GameStyleService.DefaultVoicePackKey(style);
                    preferredPreset = fallback;
                    VoicePackItem fallbackPack = visiblePacks.FirstOrDefault(
                        pack => string.Equals(pack.Key, fallback, StringComparison.OrdinalIgnoreCase));
                    if (fallbackPack == null)
                    {
                        fallbackPack = await PackCatalogService.GetVoicePackAsync(fallback);
                    }
                    else
                    {
                        visiblePacks.Remove(fallbackPack);
                    }

                    if (fallbackPack != null)
                    {
                        visiblePacks.Insert(0, fallbackPack);
                    }
                }
            }

            if (!_isPageActive || style != GameStyleService.Current) return;
            PackTestSectionView.VoicePackSelector.Items.Clear();
            int populated = 0;
            foreach (VoicePackItem pack in visiblePacks)
            {
                PackTestSectionView.VoicePackSelector.Items.Add(CreateVoicePackComboBoxItem(pack));
                if (++populated % 16 == 0)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () => { });
                    if (!_isPageActive || style != GameStyleService.Current) return;
                }
            }

            if (PackTestSectionView.VoicePackSelector.Items.Count == 0)
            {
                string fallback = GameStyleService.DefaultVoicePackKey(style);
                PackTestSectionView.VoicePackSelector.Items.Add(CreatePackComboBoxItem(
                    GetFallbackVoicePackDisplayName(fallback),
                    fallback,
                    GetVoicePackIconUri(fallback)));
            }

            SelectVoicePackPreset(preferredPreset);
        }

        private async Task PopulateIconPackSelectorAsync()
        {
            GameStyleMode style = GameStyleService.Current;
            string preferredIconPack = LoadPackSettingForStyle(
                IconPackSettingKey,
                style,
                GameStyleService.DefaultIconPackKey(style));

            var visiblePacks = (await PackCatalogService.GetVisibleIconPacksAsync()).ToList();
            if (!visiblePacks.Any(pack => string.Equals(pack.Key, preferredIconPack, StringComparison.OrdinalIgnoreCase)))
            {
                IconPackItem preferredPack = await PackCatalogService.GetIconPackAsync(preferredIconPack);
                if (preferredPack != null && GameStyleService.GetStyleForPackKey(preferredPack.Key) == style)
                {
                    visiblePacks.Insert(0, preferredPack);
                }
                else
                {
                    string fallback = GameStyleService.DefaultIconPackKey(style);
                    preferredIconPack = fallback;
                    IconPackItem fallbackPack = visiblePacks.FirstOrDefault(
                        pack => string.Equals(pack.Key, fallback, StringComparison.OrdinalIgnoreCase));
                    if (fallbackPack == null)
                    {
                        fallbackPack = await PackCatalogService.GetIconPackAsync(fallback);
                    }
                    else
                    {
                        visiblePacks.Remove(fallbackPack);
                    }

                    if (fallbackPack != null)
                    {
                        visiblePacks.Insert(0, fallbackPack);
                    }
                }
            }

            if (!_isPageActive || style != GameStyleService.Current) return;
            PackTestSectionView.IconPackSelector.Items.Clear();
            int populated = 0;
            foreach (IconPackItem pack in visiblePacks)
            {
                PackTestSectionView.IconPackSelector.Items.Add(CreateIconPackComboBoxItem(pack));
                if (++populated % 16 == 0)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () => { });
                    if (!_isPageActive || style != GameStyleService.Current) return;
                }
            }

            if (PackTestSectionView.IconPackSelector.Items.Count == 0)
            {
                string fallback = GameStyleService.DefaultIconPackKey(style);
                PackTestSectionView.IconPackSelector.Items.Add(CreatePackComboBoxItem(
                    GetFallbackIconPackDisplayName(fallback),
                    fallback,
                    GetIconPackIconUri(fallback)));
            }

            SelectIconPack(preferredIconPack);
        }

        private PackSelectorItem CreateVoicePackComboBoxItem(VoicePackItem pack)
        {
            string key = pack?.Key ?? string.Empty;
            bool customHead = pack != null && !pack.IsBuiltIn && !ValorantPackService.IsValorantPackKey(key);
            return new PackSelectorItem(PackCatalogService.GetVoicePackDisplayName(pack), key,
                GetVoicePackIconUri(key, pack?.AssociationId), customHead ? pack.FolderPath : null,
                customHead ? VoicePackHeadImageNames : null);
        }

        private PackSelectorItem CreateIconPackComboBoxItem(IconPackItem pack)
        {
            string key = pack?.Key ?? string.Empty;
            bool customHead = pack != null && !pack.IsBuiltIn && !ValorantPackService.IsValorantPackKey(key);
            return new PackSelectorItem(PackCatalogService.GetIconPackDisplayName(pack), key,
                GetIconPackIconUri(key), customHead ? pack.FolderPath : null,
                customHead ? IconPackHeadImageNames : null);
        }

        private static PackSelectorItem CreatePackComboBoxItem(string text, string tag, string iconUri)
            => new PackSelectorItem(text, tag, iconUri);

        private static string GetVoicePackIconUri(string key, string associationId = null)
        {
            if (string.Equals(key, "custommodule", StringComparison.OrdinalIgnoreCase))
            {
                return "ms-appx:///Assets/GameLogos/custommodule.png";
            }
            if (GameStyleService.IsCustomModuleKey(key)) return "ms-appx:///Assets/GameLogos/custommodule.png";
            if (ValorantPackService.IsValorantPackKey(key))
            {
                return GetValorantPackIconUri(key, associationId);
            }

            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "crossfire_swat_gr":
                case "crossfire_swat_bl":
                    return new Uri(System.IO.Path.Combine(CrossfireExternalAssetService.PackPath(key, true), "pack_head.png")).AbsoluteUri;
                case "crossfire_flying_tiger_gr":
                case "crossfire_flying_tiger_bl":
                    return new Uri(System.IO.Path.Combine(CrossfireExternalAssetService.PackPath(key, true), "pack_head.png")).AbsoluteUri;
                case "crossfire_women_gr":
                case "crossfire_women_bl":
                    return new Uri(System.IO.Path.Combine(CrossfireExternalAssetService.PackPath(key, true), "pack_head.png")).AbsoluteUri;
                case "crossfire_v_sex":
                    return new Uri(System.IO.Path.Combine(CrossfireExternalAssetService.PackPath(key, true), "pack_head.png")).AbsoluteUri;
                case "crossfire_bunny_gr":
                case "crossfire_bunny_bl":
                    return new Uri(System.IO.Path.Combine(CrossfireExternalAssetService.PackPath(key, true), "pack_head.png")).AbsoluteUri;
                case "crossfire_heart_judge_gr":
                case "crossfire_heart_judge_bl":
                    return new Uri(System.IO.Path.Combine(CrossfireExternalAssetService.PackPath(key, true), "pack_head.png")).AbsoluteUri;
                case "bf1":
                    return "ms-appx:///Assets/GameStyles/battlefield1/killconfirm/textures/killicon_battlefield1_headshot.png";
                case "bf5":
                    return "ms-appx:///Assets/GameStyles/battlefield5/killconfirm/textures/killicon_battlefield5_headshot.png";
                case "bf4":
                    return "ms-appx:///Assets/GameStyles/battlefield4/killconfirm/textures/killicon_battlefield1_headshot.png";
                case "battlefield2042":
                    return "ms-appx:///Assets/GameLogos/battlefield2042.png";
                case "pubg":
                    return "ms-appx:///Assets/GameStyles/pubg/killconfirm/textures/killicon_scrolling_headshot.png";
                case "deltaforce":
                    return "ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_headshot.png";
                case "doubao":
                    return "ms-appx:///Assets/GameLogos/doubao.png";
                case "dagoujiao":
                    return "ms-appx:///Assets/GameLogos/dagoujiao.jpg";
                case "dagoujiao_animals":
                    return "ms-appx:///Assets/GameStyles/dagoujiao/iconpacks/dagoujiao_animals/animals.jpg";
                case "overwatch":
                    return "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/preview.png";
                case "modernwarfare2019":
                    return "ms-appx:///Assets/GameLogos/modernwarfare2019.png";
                case "apex":
                    return "ms-appx:///Assets/GameLogos/apex.png";
                case "csol4":
                    return "ms-appx:///Assets/KillConfirmCode/Csol4/headshot_kill.png";
                default:
                    break;
            }

            switch (GameStyleService.GetStyleForPackKey(key))
            {
                case GameStyleMode.Csol:
                    return "ms-appx:///Assets/KillConfirmCode/Csol4/headshot_kill.png";
                case GameStyleMode.Valorant:
                    return GetValorantPackIconUri(ValorantPackService.DefaultKey);
                case GameStyleMode.Battlefield1:
                    return "ms-appx:///Assets/GameStyles/battlefield1/killconfirm/textures/killicon_battlefield1_default.png";
                case GameStyleMode.Battlefield5:
                    return "ms-appx:///Assets/GameStyles/battlefield5/killconfirm/textures/killicon_battlefield5_default.png";
                case GameStyleMode.Battlefield4:
                    return "ms-appx:///Assets/GameStyles/battlefield4/killconfirm/textures/killicon_battlefield1_default.png";
                case GameStyleMode.Battlefield2042:
                    return "ms-appx:///Assets/GameLogos/battlefield2042.png";
                case GameStyleMode.Pubg:
                    return "ms-appx:///Assets/GameStyles/pubg/killconfirm/textures/killicon_scrolling_default.png";
                case GameStyleMode.DeltaForce:
                    return "ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_default.png";
                case GameStyleMode.Doubao:
                    return "ms-appx:///Assets/GameLogos/doubao.png";
                case GameStyleMode.Dagoujiao:
                    return "ms-appx:///Assets/GameLogos/dagoujiao.jpg";
                case GameStyleMode.Overwatch:
                    return "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/preview.png";
                case GameStyleMode.ModernWarfare2019:
                    return "ms-appx:///Assets/GameLogos/modernwarfare2019.png";
                case GameStyleMode.Apex:
                    return "ms-appx:///Assets/GameLogos/apex.png";
                default:
                    return CrossfireExternalAssetService.VisualUri("Original", "badge_headshot.PNG");
            }
        }
    }
}
