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
        private const string CloseBehaviorKey = "CloseWindowBehavior";
        private readonly MediaPlayer _previewPlayer = new MediaPlayer();
        private bool _iconSpecExpanded;
        private bool _suppressCloseBehaviorEvents;

        public MainPage()
        {
            InitializeComponent();
            ApplyLanguage();
            LoadCloseBehaviorSetting();
            GameStyleService.Changed += OnGameStyleServiceChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void LoadCloseBehaviorSetting()
        {
            if (CloseBehaviorSelector == null)
            {
                return;
            }

            _suppressCloseBehaviorEvents = true;
            try
            {
                string value = ApplicationData.Current.LocalSettings.Values[CloseBehaviorKey] as string;
                string targetTag = string.Equals(value, "exit", StringComparison.OrdinalIgnoreCase) ? "exit" : "tray";
                foreach (object item in CloseBehaviorSelector.Items)
                {
                    if (item is ComboBoxItem comboItem && comboItem.Tag is string tag && string.Equals(tag, targetTag, StringComparison.OrdinalIgnoreCase))
                    {
                        CloseBehaviorSelector.SelectedItem = comboItem;
                        break;
                    }
                }
            }
            finally
            {
                _suppressCloseBehaviorEvents = false;
            }
        }

        private void OnCloseBehaviorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCloseBehaviorEvents)
            {
                return;
            }

            if (CloseBehaviorSelector?.SelectedItem is ComboBoxItem selected && selected.Tag is string mode)
            {
                ApplicationData.Current.LocalSettings.Values[CloseBehaviorKey] = mode;
            }
        }

        private void OnGameStyleServiceChanged(object sender, GameStyleMode mode)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ApplyGameStyleUi();
            });
        }
    }
}
