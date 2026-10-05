using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Services;
using System;
using Windows.Storage;

namespace KillConfirmCompatibility
{
    internal sealed partial class FeedbackPresenter
    {
        public readonly KillConfirmAnimation LowerFeedbackAnimation = new();
        public readonly KillConfirmAnimation LowerBadgeAnimation = new();
        public readonly KillConfirmAnimation CrosshairFeedbackAnimation = new();
        public readonly KillConfirmAnimation UpperFeedbackAnimation = new();
        public Action<KillEvent> DanmakuEvent;
        public string ConfigurationError { get; private set; }
        private string GetSelectedIconPack() => ReadPack("KillIconPack", GameStyleService.DefaultIconPackKey(GameStyleService.Current));
        private string GetSelectedVoicePackPreset() => ReadPack("VoicePack", GameStyleService.DefaultVoicePackKey(GameStyleService.Current));
        private static string ReadPack(string key, string fallback)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            string selected = values[key + "." + GameStyleService.ToStorageValue(GameStyleService.Current)] as string;
            return string.IsNullOrWhiteSpace(selected) ? fallback : selected;
        }
        public async System.Threading.Tasks.Task ApplyConfigurationAsync()
        {
            LowerFeedbackAnimation.ReleaseAnimationResourcesForPackChange();
            LowerBadgeAnimation.ReleaseAnimationResourcesForPackChange();
            CrosshairFeedbackAnimation.ReleaseAnimationResourcesForPackChange();
            UpperFeedbackAnimation.ReleaseAnimationResourcesForPackChange();
            string pack = GetSelectedIconPack();
            var requestStyle = GameStyleService.Current;
            KillConfirmAnimation.ConfigureIconPack(pack);
            var values = ApplicationData.Current.LocalSettings.Values;
            KillConfirmAnimation.ConfigureEliteEffectLevel(values["KillEliteEffect"] is int elite ? elite : 0);
            KillConfirmAnimation.ConfigureWeaponBadgeMode(values["KillWeaponBadge"] is int badge ? badge : 0);
            KillConfirmAnimation.ConfigureMainAnimationStyle(values["MainAnimationStyle"] is int style ? style : 1);
            KillConfirmAnimation.ConfigurePlaybackFps(60);
            try
            {
                await PackCatalogService.ReloadForCompatibilityAsync();
                bool imported = PackCatalogService.IsImportedIconPackKey(pack);
                IconPackItem item = imported ? await PackCatalogService.RefreshImportedIconPackCapabilitiesAsync(pack) : null;
                if (GetSelectedIconPack() != pack || requestStyle != GameStyleService.Current) return;
                KillConfirmAnimation.ConfigureCustomPackOverlayCapabilities(item?.HasKillFxOverlay == true, item?.HasEliteOverlay == true, item?.HasWeaponBadgeOverlay == true);
                KillConfirmAnimation.ConfigureKillFxMode(values["KillFxEnabled"] is int fx ? fx : imported ? (item?.HasKillFxOverlay == true ? 1 : 0) : 1);
                ConfigurationError = GameStyleService.Current == GameStyleMode.Crossfire && await PackCatalogService.GetImportedIconFolderAsync(pack) == null
                    ? "当前 CF 图标包未安装，请在图标库导入资源包。" : null;
            }
            catch (Exception error) { ConfigurationError = error.Message; App.Log("Compatibility pack configuration: " + error); }
        }
    }
}
