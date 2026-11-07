using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
    {
        private void LoadRasterQuality()
        {
            var quality = KillConfirmCompatibility.Contracts.AnimationRasterPolicy.ReadForGame(ApplicationData.Current.LocalSettings.Values, GameStyleService.ToStorageValue(GameStyleService.Current));
            bool previous = _suppressVisualAdjustmentEvents;
            _suppressVisualAdjustmentEvents = true;
            foreach (ComboBoxItem item in PackTestSectionView.RasterQualitySelector.Items)
                if ((string)item.Tag == quality.ToString()) { PackTestSectionView.RasterQualitySelector.SelectedItem = item; break; }
            _suppressVisualAdjustmentEvents = previous;
        }
        private void OnRasterQualitySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressVisualAdjustmentEvents || !(PackTestSectionView.RasterQualitySelector.SelectedItem is ComboBoxItem selected)) return;
            int quality = KillConfirmCompatibility.Contracts.AnimationRasterPolicy.Normalize(selected.Tag);
            ApplicationData.Current.LocalSettings.Values[KillConfirmCompatibility.Contracts.AnimationRasterPolicy.GameSettingKey(GameStyleService.ToStorageValue(GameStyleService.Current))] = quality;
        }
        private void LoadVolume()
        {
            LoadRasterQuality();
            _suppressVisualAdjustmentEvents = true;
            double volume = ReadDoubleSetting(ApplicationData.Current.LocalSettings, GetAnimationStyleSettingKey(AudioVolumeSettingKey), ReadDoubleSetting(ApplicationData.Current.LocalSettings, AudioVolumeSettingKey, 100));
            foreach (object option in PackTestSectionView.AudioVolumeSelector.Items)
                if (option is ComboBoxItem item && double.TryParse(item.Tag as string, out double value) && Math.Abs(value - Math.Round(volume / 10) * 10) < 0.1) { PackTestSectionView.AudioVolumeSelector.SelectedItem = item; break; }
            _suppressVisualAdjustmentEvents = false;
        }
        private async void OnAudioVolumeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressVisualAdjustmentEvents || !_packSelectorsInitialized || !double.TryParse((PackTestSectionView.AudioVolumeSelector.SelectedItem as ComboBoxItem)?.Tag as string, out double volume)) return;
            ApplicationData.Current.LocalSettings.Values[GetAnimationStyleSettingKey(AudioVolumeSettingKey)] = volume;
            try
            {
                using (var client = await LocalServiceAuth.CreateHttpClientAsync()) using (var body = new HttpStringContent("{\"percent\":" + (int)Math.Max(0, Math.Min(200, volume)) + "}", Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json")) using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/audio/volume"), body)) response.EnsureSuccessStatusCode();
            }
            catch (Exception error) { App.Log("Compatibility volume: " + error.Message); }
        }
        private static string GetAnimationStyleSettingKey(string key) => key + "." + GameStyleService.Current;
        private static double ReadDoubleSetting(ApplicationDataContainer settings, string key, double fallback)
        {
            object raw = settings.Values[key];
            if (raw is double d) return d; if (raw is int i) return i; if (raw is float f) return f; return fallback;
        }
    }
}
