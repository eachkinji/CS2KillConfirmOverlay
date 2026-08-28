using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class CustomModuleSettings
    {
        public int Fps; // 0: use the material's FPS
        public double Hold = -1; // -1: use the material's hold
        public bool Fade = true;
        public bool Headshots = true;
    }

    internal static class CustomModuleSettingsStore
    {
        public static event EventHandler Changed;
        public static CustomModuleSettings Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return new CustomModuleSettings
            {
                Fps = values.TryGetValue("CustomModule.Fps", out object fps) && fps is int f ? Math.Max(0, Math.Min(60, f)) : 0,
                Hold = values.TryGetValue("CustomModule.Hold", out object hold) && hold is double h ? Math.Max(-1, Math.Min(10, h)) : -1,
                Fade = !(values.TryGetValue("CustomModule.Fade", out object fade) && fade is bool b) || b,
                Headshots = !(values.TryGetValue("CustomModule.Headshots", out object hs) && hs is bool enabled) || enabled
            };
        }

        public static void Save(CustomModuleSettings settings)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values["CustomModule.Fps"] = settings.Fps;
            values["CustomModule.Hold"] = settings.Hold;
            values["CustomModule.Fade"] = settings.Fade;
            values["CustomModule.Headshots"] = settings.Headshots;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
