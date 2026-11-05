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

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private int _lastCrossfirePackRevision = -1;
        private int _lastValorantEditRevision;

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
                    if (!_isPageActive) return;
                    App.NoteAction("catalog refresh style=" + GameStyleService.Current);
                    try
                    {
                        bool valorantEdited = GameStyleService.Current == GameStyleMode.Valorant
                            && _lastValorantEditRevision != ValorantPackEditing.Revision;
                        if (valorantEdited)
                        {
                            _lastValorantEditRevision = ValorantPackEditing.Revision;
                            LowerFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                            LowerBadgeAnimation?.ReleaseAnimationResourcesForPackChange();
                            CrosshairFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                            UpperFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                        }
                        if (GameStyleService.Current == GameStyleMode.Crossfire
                            && _lastCrossfirePackRevision != CrossfireExternalAssetService.Revision)
                        {
                            _lastCrossfirePackRevision = CrossfireExternalAssetService.Revision;
                            LowerFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                            LowerBadgeAnimation?.ReleaseAnimationResourcesForPackChange();
                            CrosshairFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                            UpperFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                        }
                        QueueGameStyleRefresh();
                        App.NoteAction("catalog refresh done");
                    }
                    catch (Exception ex)
                    {
                        App.LogCrash("Pack catalog refresh failed after [" + "style=" + GameStyleService.Current + "]: " + ex);
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
            App.NoteAction("initialize selectors style=" + requestStyle);
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
                // An explicitly disabled or temporarily missing sound pack stays disabled on reload.
                if (GetSelectedVoicePackPreset() != "none")
                    await TrySyncSoundEffectForIconSelectionAsync(GetSelectedIconPack());
                ApplyGameStyleUi();
                _packSelectorsInitialized = true;
                App.NoteAction("selectors initialized voice=" + PackTestSectionView.VoicePackSelector.Items.Count
                    + " icon=" + PackTestSectionView.IconPackSelector.Items.Count);
                if (_isPageActive)
                {
                    _ = WarmStartupAnimationCacheAsync(0);
                }
            }
            catch (Exception ex)
            {
                App.LogCrash("Initialize pack selectors failed: " + ex);
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
            if (Features.CompatibilityDisplay.CompatibilityDisplayRuntime.IsEnabled) return;
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
                        StartKillEventClient();
                    }
                }
            }
        }

        private async Task PopulateVoicePackSelectorAsync()
        {
            await PopulateAudioPackSelectorsAsync();
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
            foreach (IconPackItem pack in visiblePacks)
            {
                PackTestSectionView.IconPackSelector.Items.Add(CreateIconPackComboBoxItem(pack));
            }

            if (PackTestSectionView.IconPackSelector.Items.Count == 0)
            {
                string fallback = GameStyleService.DefaultIconPackKey(style);
                PackTestSectionView.IconPackSelector.Items.Add(CreatePackComboBoxItem(
                    GetFallbackIconPackDisplayName(fallback),
                    fallback,
                    GetIconPackIconUri(fallback)));
            }

            PackTestSectionView.IconPackSelector.Items.Add(CreateAddMorePackItem(false));
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

        private static PackSelectorItem CreateAddMorePackItem(bool voice)
        {
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string text = chinese
                ? (voice ? "＋ 添加更多音效包" : "＋ 添加更多图标包")
                : (voice ? "+ Add more sound effect packs" : "+ Add more icon packs");
            return CreatePackComboBoxItem(text, PackLibraryNavigation.AddMoreTag, null);
        }

        private async Task OpenPackLibraryAsync(bool voice, SelectionChangedEventArgs e)
        {
            App.NoteAction("open pack library voice=" + voice);
            var selector = voice ? PackTestSectionView.VoicePackSelector : PackTestSectionView.IconPackSelector;
            bool previous = voice ? _suppressVoicePackEvents : _suppressIconPackEvents;
            if (voice) _suppressVoicePackEvents = true;
            else _suppressIconPackEvents = true;
            try
            {
                // This is a navigation action; restore the real selection before any await.
                PackSelectorItem restore = e.RemovedItems.OfType<PackSelectorItem>()
                    .FirstOrDefault(item => !Equals(item.Tag, PackLibraryNavigation.AddMoreTag)
                        && selector.Items.Contains(item))
                    ?? selector.Items.OfType<PackSelectorItem>()
                        .FirstOrDefault(item => !Equals(item.Tag, PackLibraryNavigation.AddMoreTag));
                if (restore != null) selector.SelectedItem = restore;
            }
            catch (Exception ex)
            {
                App.LogCrash("Pack library selection restore failed: " + ex);
            }
            finally
            {
                if (voice) _suppressVoicePackEvents = previous;
                else _suppressIconPackEvents = previous;
            }
            try
            {
                await PackLibraryNavigation.RequestSharedAsync(GameStyleService.Current, voice);
                string group = DeveloperModeSettingsStore.IsEnabled
                    ? OpenSettingsWindowDeveloperParameterGroupId : OpenSettingsWindowParameterGroupId;
                if (await TryLaunchFullTrustHelperAsync(group)) return;
            }
            catch (Exception ex) { App.Log("Open pack library failed: " + ex.Message); }
            PackLibraryNavigation.Clear();
            ShowGuideOpenFailedHint();
        }

        private static string GetVoicePackIconUri(string key, string associationId = null)
        {
            if (string.Equals(key, "custommodule", StringComparison.OrdinalIgnoreCase))
            {
                return SharedResources.CachedAssetUri("Assets/GameLogos/custommodule.png");
            }
            if (GameStyleService.IsCustomModuleKey(key)) return null;
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
                    return SharedResources.CachedAssetUri("Assets/GameStyles/battlefield1/killconfirm/textures/killicon_battlefield1_headshot.png");
                case "bf5":
                    return SharedResources.CachedAssetUri("Assets/GameStyles/battlefield5/killconfirm/textures/killicon_battlefield5_headshot.png");
                case "bf4":
                    return SharedResources.CachedAssetUri("Assets/GameStyles/battlefield4/killconfirm/textures/killicon_battlefield1_headshot.png");
                case "battlefield2042":
                    return SharedResources.CachedAssetUri("Assets/GameLogos/battlefield2042.png");
                case "battlefield6":
                    return SharedResources.CachedAssetUri("Assets/GameStyles/battlefield6/killconfirm/textures/killicon_battlefield6_default.png");
                case "pubg":
                    return SharedResources.CachedAssetUri("Assets/GameStyles/pubg/killconfirm/textures/killicon_scrolling_headshot.png");
                case "deltaforce":
                    return SharedResources.CachedAssetUri("Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_headshot.png");
                case "calabiyau":
                    return SharedResources.CachedAssetUri("Assets/GameLogos/calabiyau.png");
                case "doubao":
                    return SharedResources.CachedAssetUri("Assets/GameLogos/doubao.png");
                case "dagoujiao":
                    return SharedResources.CachedAssetUri("Assets/GameLogos/dagoujiao.jpg");
                case "dagoujiao_animals":
                    return SharedResources.CachedAssetUri("Assets/GameStyles/dagoujiao/iconpacks/dagoujiao_animals/animals.jpg");
                case "overwatch":
                    return SharedResources.CachedAssetUri("Assets/GameStyles/overwatch/killconfirm/textures/preview.png");
                case "modernwarfare2019":
                    return SharedResources.CachedAssetUri("Assets/GameLogos/modernwarfare2019.png");
                case "apex":
                    return SharedResources.CachedAssetUri("Assets/GameLogos/apex.png");
                case "csol4":
                    return SharedResources.CachedAssetUri("Assets/KillConfirmCode/Csol4/headshot_kill.png");
                default:
                    break;
            }

            switch (GameStyleService.GetStyleForPackKey(key))
            {
                case GameStyleMode.Csol:
                    return SharedResources.CachedAssetUri("Assets/KillConfirmCode/Csol4/headshot_kill.png");
                case GameStyleMode.Valorant:
                    return GetValorantPackIconUri(ValorantPackService.DefaultKey);
                case GameStyleMode.Battlefield1:
                    return SharedResources.CachedAssetUri("Assets/GameStyles/battlefield1/killconfirm/textures/killicon_battlefield1_default.png");
                case GameStyleMode.Battlefield5:
                    return SharedResources.CachedAssetUri("Assets/GameStyles/battlefield5/killconfirm/textures/killicon_battlefield5_default.png");
                case GameStyleMode.Battlefield4:
                    return SharedResources.CachedAssetUri("Assets/GameStyles/battlefield4/killconfirm/textures/killicon_battlefield1_default.png");
                case GameStyleMode.Battlefield2042:
                    return SharedResources.CachedAssetUri("Assets/GameLogos/battlefield2042.png");
                case GameStyleMode.Battlefield6:
                    return SharedResources.CachedAssetUri("Assets/GameStyles/battlefield6/killconfirm/textures/killicon_battlefield6_default.png");
                case GameStyleMode.Pubg:
                    return SharedResources.CachedAssetUri("Assets/GameStyles/pubg/killconfirm/textures/killicon_scrolling_default.png");
                case GameStyleMode.DeltaForce:
                    return SharedResources.CachedAssetUri("Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_default.png");
                case GameStyleMode.Doubao:
                    return SharedResources.CachedAssetUri("Assets/GameLogos/doubao.png");
                case GameStyleMode.Dagoujiao:
                    return SharedResources.CachedAssetUri("Assets/GameLogos/dagoujiao.jpg");
                case GameStyleMode.Overwatch:
                    return SharedResources.CachedAssetUri("Assets/GameStyles/overwatch/killconfirm/textures/preview.png");
                case GameStyleMode.ModernWarfare2019:
                    return SharedResources.CachedAssetUri("Assets/GameLogos/modernwarfare2019.png");
                case GameStyleMode.Apex:
                    return SharedResources.CachedAssetUri("Assets/GameLogos/apex.png");
                default:
                    return CrossfireExternalAssetService.VisualUri("Original", "badge_headshot.PNG");
            }
        }
    }
}
