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

        private static string GetIconPackIconUri(string key)
        {
            if (string.Equals(key, "custommodule", StringComparison.OrdinalIgnoreCase))
            {
                return "ms-appx:///Assets/GameStyles/custommodule/iconpacks/custommodule/pack_head.webp";
            }
            if (GameStyleService.IsCustomModuleKey(key)) return null;
            if (ValorantPackService.IsValorantPackKey(key))
            {
                return GetValorantPackIconUri(key);
            }

            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vip":
                    return CrossfireExternalAssetService.VisualUri("Vip", "badge_headshot.png");
                case "angelic_beast":
                    return CrossfireExternalAssetService.VisualUri("AngelicBeast", "badge_headshot.png");
                case "anniversary_10":
                    return CrossfireExternalAssetService.VisualUri("Anniversary10", "badge_headshot.png");
                case "anniversary_15":
                    return CrossfireExternalAssetService.VisualUri("Anniversary15", "badge_headshot.png");
                case "cfpl":
                    return CrossfireExternalAssetService.VisualUri("CFPL", "badge_headshot.png");
                case "rankmach_2019_1":
                    return CrossfireExternalAssetService.VisualUri("Rankmach2019_1", "badge_headshot.png");
                case "rankmach_2019_2":
                    return CrossfireExternalAssetService.VisualUri("Rankmach2019_2", "badge_headshot.png");
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
                case "default":
                default:
                    return CrossfireExternalAssetService.VisualUri("Original", "badge_headshot.PNG");
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
                case "custommodule":
                    return "瓦默认音效/图标";
                case "doubao":
                    return "豆包";
                case "dagoujiao":
                    return "大狗叫";
                case "overwatch":
                    return "OverWatch";
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
                case "custommodule":
                    return "瓦默认音效/图标";
                case "doubao":
                    return "豆包";
                case "dagoujiao":
                    return "大狗叫";
                case "overwatch":
                    return "OverWatch";
                case "default":
                    return "原版";
                case "csol4":
                    return "CSOL 10杀";
                default:
                    return ValorantPackService.IsValorantPackKey(key)
                        ? ValorantPackService.GetDisplayName(key)
                        : key;
            }
        }

        private static string GetValorantPackIconUri(string key)
        {
            string emblemUri = ValorantPackService.GetEmblemUri(key);
            if (!string.IsNullOrWhiteSpace(emblemUri))
            {
                return emblemUri;
            }

            // Fallback for custom Valorant packs (custom_valorant_voice_*) which have no
            // declared emblem: use the pack's headshot texture, then the default pack's.
            return "ms-appx:///Assets/GameStyles/valorant/killconfirm/_native/shared/textures/Base_headshot.png";
        }
    }
}
