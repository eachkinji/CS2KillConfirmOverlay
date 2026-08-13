using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using KillConfirmGameBar.Helpers;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage : Page
    {
        private readonly MediaPlayer _previewPlayer = new MediaPlayer();
        private bool _iconSpecExpanded;

        public MainPage()
        {
            InitializeComponent();
            ApplyLanguage();
            GameStyleService.Changed += OnGameStyleServiceChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnGameStyleServiceChanged(object sender, GameStyleMode mode)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                ApplyGameStyleUi();
                try
                {
                    await CombatEventSoundSettingsStore.SyncAsync(mode);
                }
                catch (System.Exception ex)
                {
                    App.Log("Sync event sounds after style change failed: " + ex.Message);
                }
            });
        }
    }
}
