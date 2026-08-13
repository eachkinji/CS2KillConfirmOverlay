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
        private async void OnPackCatalogChanged(object sender, EventArgs e)
        {
            if (!_isPageActive)
            {
                return;
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await InitializePackSelectorsAsync();
            });
        }

        private async Task InitializePackSelectorsAsync()
        {
            await _packSelectorInitializationLock.WaitAsync();
            bool previousVoiceSuppression = _suppressVoicePackEvents;
            bool previousIconSuppression = _suppressIconPackEvents;
            _suppressVoicePackEvents = true;
            _suppressIconPackEvents = true;
            try
            {
                await PopulateVoicePackSelectorAsync();
                await PopulateIconPackSelectorAsync();
                LoadIconPackSetting();
                LoadEliteEffectSetting();
                LoadKillFxSetting();
                LoadWeaponBadgeSetting();
                LoadMainAnimationStyleSetting();
                LoadVoicePackSetting();
                ApplyGameStyleUi();
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
                await EnsureServiceAvailableAsync();
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

            VoicePackSelector.Items.Clear();
            foreach (VoicePackItem pack in visiblePacks)
            {
                VoicePackSelector.Items.Add(await CreateVoicePackComboBoxItemAsync(pack));
            }

            if (VoicePackSelector.Items.Count == 0)
            {
                string fallback = GameStyleService.DefaultVoicePackKey(style);
                VoicePackSelector.Items.Add(CreatePackComboBoxItem(
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

            IconPackSelector.Items.Clear();
            foreach (IconPackItem pack in visiblePacks)
            {
                IconPackSelector.Items.Add(await CreateIconPackComboBoxItemAsync(pack));
            }

            if (IconPackSelector.Items.Count == 0)
            {
                string fallback = GameStyleService.DefaultIconPackKey(style);
                IconPackSelector.Items.Add(CreatePackComboBoxItem(
                    GetFallbackIconPackDisplayName(fallback),
                    fallback,
                    GetIconPackIconUri(fallback)));
            }

            SelectIconPack(preferredIconPack);
        }

        private async Task<ComboBoxItem> CreateVoicePackComboBoxItemAsync(VoicePackItem pack)
        {
            string key = pack?.Key ?? string.Empty;
            ComboBoxItem item = CreatePackComboBoxItem(
                PackCatalogService.GetVoicePackDisplayName(pack),
                key,
                GetVoicePackIconUri(key));

            if (pack != null && !pack.IsBuiltIn)
            {
                Image image = item.Tag as string != null ? FindPackItemImage(item) : null;
                await TryApplyPackFolderImageAsync(image, pack.FolderPath, VoicePackHeadImageNames);
            }

            return item;
        }

        private async Task<ComboBoxItem> CreateIconPackComboBoxItemAsync(IconPackItem pack)
        {
            string key = pack?.Key ?? string.Empty;
            ComboBoxItem item = CreatePackComboBoxItem(
                PackCatalogService.GetIconPackDisplayName(pack),
                key,
                GetIconPackIconUri(key));

            if (pack != null && !pack.IsBuiltIn)
            {
                Image image = FindPackItemImage(item);
                await TryApplyPackFolderImageAsync(image, pack.FolderPath, IconPackHeadImageNames);
            }

            return item;
        }

        private static ComboBoxItem CreatePackComboBoxItem(string text, string tag, string iconUri)
        {
            GameThemePalette theme = GameThemePalette.Current;
            var image = new Image
            {
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };

            if (!string.IsNullOrWhiteSpace(iconUri))
            {
                image.Source = new BitmapImage(new Uri(iconUri));
            }

            var label = new TextBlock
            {
                Text = text ?? string.Empty,
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.Text),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(image);
            panel.Children.Add(label);

            return new ComboBoxItem
            {
                Content = panel,
                Tag = tag,
                Foreground = new SolidColorBrush(theme.Text),
                Background = new SolidColorBrush(theme.Field),
                BorderBrush = new SolidColorBrush(theme.Border)
            };
        }

        private static Image FindPackItemImage(ComboBoxItem item)
        {
            if (item?.Content is StackPanel panel)
            {
                return panel.Children.OfType<Image>().FirstOrDefault();
            }

            return null;
        }

        private static async Task TryApplyPackFolderImageAsync(Image image, string folderPath, IReadOnlyList<string> candidateNames)
        {
            if (image == null || string.IsNullOrWhiteSpace(folderPath) || candidateNames == null)
            {
                return;
            }

            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                foreach (string candidateName in candidateNames)
                {
                    StorageFile file = await TryGetNestedFileAsync(folder, candidateName);
                    if (file == null)
                    {
                        continue;
                    }

                    var bitmap = new BitmapImage();
                    using (IRandomAccessStream stream = await file.OpenReadAsync())
                    {
                        await bitmap.SetSourceAsync(stream);
                    }

                    image.Source = bitmap;
                    return;
                }
            }
            catch
            {
            }
        }

        private static async Task<StorageFile> TryGetNestedFileAsync(StorageFolder root, string relativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            try
            {
                string[] parts = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                StorageFolder folder = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    folder = await folder.GetFolderAsync(parts[i]);
                }

                return await folder.GetFileAsync(parts[parts.Length - 1]);
            }
            catch
            {
                return null;
            }
        }

        private static string GetVoicePackIconUri(string key)
        {
            if (ValorantPackService.IsValorantPackKey(key))
            {
                return GetValorantPackIconUri(key);
            }

            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "crossfire_swat_gr":
                case "crossfire_swat_bl":
                    return "ms-appx:///Assets/PackIcons/swat.png";
                case "crossfire_flying_tiger_gr":
                case "crossfire_flying_tiger_bl":
                    return "ms-appx:///Assets/PackIcons/flying_tiger.png";
                case "crossfire_women_gr":
                case "crossfire_women_bl":
                    return "ms-appx:///Assets/PackIcons/women.png";
                case "crossfire_v_sex":
                    return "ms-appx:///Assets/PackIcons/cfsex.png";
                case "crossfire_bunny_gr":
                case "crossfire_bunny_bl":
                    return "ms-appx:///Assets/PackIcons/bunny.png";
                case "crossfire_heart_judge_gr":
                case "crossfire_heart_judge_bl":
                    return "ms-appx:///Assets/PackIcons/heart_judge.png";
                case "bf1":
                    return "ms-appx:///Assets/GameStyles/battlefield1/killconfirm/textures/killicon_battlefield1_headshot.png";
                case "bf5":
                    return "ms-appx:///Assets/GameStyles/battlefield5/killconfirm/textures/killicon_battlefield5_headshot.png";
                case "bf4":
                    return "ms-appx:///Assets/GameStyles/battlefield4/killconfirm/textures/killicon_battlefield1_headshot.png";
                case "battlefield2042":
                    return "ms-appx:///Assets/GameStyles/battlefield2042/killconfirm/textures/HeadshotSkull.png";
                case "pubg":
                    return "ms-appx:///Assets/GameStyles/pubg/killconfirm/textures/killicon_scrolling_headshot.png";
                case "deltaforce":
                    return "ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_headshot.png";
                case "csol4":
                    return "ms-appx:///Assets/GameLogos/csol.png";
                default:
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
            }
        }

        private static string GetIconPackIconUri(string key)
        {
            if (ValorantPackService.IsValorantPackKey(key))
            {
                return GetValorantPackIconUri(key);
            }

            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vip":
                    return "ms-appx:///Assets/KillConfirmCode/Vip/badge_headshot.png";
                case "angelic_beast":
                    return "ms-appx:///Assets/KillConfirmCode/AngelicBeast/badge_headshot.png";
                case "anniversary_10":
                    return "ms-appx:///Assets/KillConfirmCode/Anniversary10/badge_headshot.png";
                case "anniversary_15":
                    return "ms-appx:///Assets/KillConfirmCode/Anniversary15/badge_headshot.png";
                case "cfpl":
                    return "ms-appx:///Assets/KillConfirmCode/CFPL/badge_headshot.png";
                case "rankmach_2019_1":
                    return "ms-appx:///Assets/KillConfirmCode/Rankmach2019_1/badge_headshot.png";
                case "rankmach_2019_2":
                    return "ms-appx:///Assets/KillConfirmCode/Rankmach2019_2/badge_headshot.png";
                case "bf1":
                    return "ms-appx:///Assets/GameStyles/battlefield1/killconfirm/textures/killicon_battlefield1_headshot.png";
                case "bf5":
                    return "ms-appx:///Assets/GameStyles/battlefield5/killconfirm/textures/killicon_battlefield5_headshot.png";
                case "bf4":
                    return "ms-appx:///Assets/GameStyles/battlefield4/killconfirm/textures/killicon_battlefield1_headshot.png";
                case "battlefield2042":
                    return "ms-appx:///Assets/GameStyles/battlefield2042/killconfirm/textures/HeadshotSkull.png";
                case "pubg":
                    return "ms-appx:///Assets/GameStyles/pubg/killconfirm/textures/killicon_scrolling_headshot.png";
                case "deltaforce":
                    return "ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_headshot.png";
                case "csol4":
                    return "ms-appx:///Assets/GameLogos/csol.png";
                case "legacy":
                case "default":
                default:
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
            }
        }

        private static string GetFallbackVoicePackDisplayName(string key)
        {
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "bf1":
                    return "Battlefield 1";
                case "bf5":
                    return "Battlefield 5";
                case "bf4":
                    return "Battlefield 4";
                case "battlefield2042":
                    return "Battlefield 2042";
                case "pubg":
                    return "PUBG";
                case "deltaforce":
                    return "Delta Force";
                case "crossfire_swat_gr":
                    return "swat GR";
                case "csol4":
                    return "CSOL 10杀";
                default:
                    return ValorantPackService.IsValorantPackKey(key)
                        ? ValorantPackService.GetDisplayName(key)
                        : key;
            }
        }

        private static string GetFallbackIconPackDisplayName(string key)
        {
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "bf1":
                    return "Battlefield 1";
                case "bf5":
                    return "Battlefield 5";
                case "bf4":
                    return "Battlefield 4";
                case "battlefield2042":
                    return "Battlefield 2042";
                case "pubg":
                    return "PUBG";
                case "deltaforce":
                    return "Delta Force";
                case "default":
                    return "\u539f\u7248";
                case "csol4":
                    return "CSOL 10\u6740";
                default:
                    return ValorantPackService.IsValorantPackKey(key)
                        ? ValorantPackService.GetDisplayName(key)
                        : key;
            }
        }

        private static string GetValorantPackIconUri(string key)
        {
            string folder = ValorantPackService.GetFolder(key) ?? "00009_prime";
            return $"ms-appx:///Assets/GameStyles/valorant/killconfirm/{folder}/textures/killicon_valorant_headshot.png";
        }
    }
}
