using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static Image CreatePackPreviewImage(string uri)
        {
            var image = new Image
            {
                Width = 42,
                Height = 42,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrWhiteSpace(uri))
            {
                image.Source = new BitmapImage(new Uri(uri));
            }

            return image;
        }

        private static async Task TryApplyCustomPackPreviewAsync(Image image, string folderPath, IReadOnlyList<string> candidateNames)
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

                    await SetPreviewImageAsync(image, file);
                    return;
                }
            }
            catch
            {
            }
        }

        private static async Task<StorageFile> TryGetCustomPackHeadImageAsync(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return null;
            }

            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                foreach (string candidateName in VoicePackHeadImageNames)
                {
                    StorageFile file = await TryGetNestedFileAsync(folder, candidateName);
                    if (file != null)
                    {
                        return file;
                    }
                }
            }
            catch
            {
            }

            return null;
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

        private static string GetVoicePackIconUri(VoicePackItem item)
        {
            switch ((item?.Key ?? string.Empty).Trim().ToLowerInvariant())
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
                default:
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
            }
        }

        private static string GetIconPackIconUri(IconPackItem item)
        {
            switch ((item?.Key ?? string.Empty).Trim().ToLowerInvariant())
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
                case "legacy":
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
                case "default":
                default:
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
            }
        }

        private async Task<StorageFile> GetBuiltInCommonOverlayFileAsync()
        {
            try
            {
                return await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///KillConfirmService/sounds/crossfire_swat_gr/common.wav"));
            }
            catch
            {
                return null;
            }
        }

        private static async Task<StorageFile> TryGetAudioFileAsync(StorageFolder folder, string baseName)
        {
            foreach (string extension in new[] { ".wav", ".mp3", ".m4a" })
            {
                StorageFile file = await TryGetFileAsync(folder, baseName + extension);
                if (file != null)
                {
                    return file;
                }
            }

            return null;
        }

        private async Task PlayPreviewAsync(StorageFile file)
        {
            if (file == null)
            {
                return;
            }

            try
            {
                _previewPlayer.Pause();
                _previewPlayer.Source = MediaSource.CreateFromStorageFile(file);
                _previewPlayer.Play();
            }
            catch
            {
                await Task.CompletedTask;
            }
        }
    }
}
