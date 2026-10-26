using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        // Data items, not UIElement items or replacement item containers: GridView owns
        // its GridViewItem containers, virtualization, drag visual and reorder animation.
        public sealed class PackCardEntry : INotifyPropertyChanged
        {
            public VoicePackItem Voice { get; set; }
            public IconPackItem Icon { get; set; }
            public string Key => Voice?.Key ?? Icon?.Key;
            public bool IsVoice => Voice != null;
            public int Ordinal { get; set; }
            public string OrdinalText => Ordinal.ToString("D2");
            public event PropertyChangedEventHandler PropertyChanged;
            internal void UpdateOrdinal(int ordinal)
            {
                Ordinal = ordinal;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OrdinalText)));
            }
        }

        private sealed class PackCardHostLoad
        {
            public PackCardEntry Entry;
            public Task Loading;
        }

        private GridView _nativePackDragGrid;
        private ObservableCollection<PackCardEntry> _nativePackDragItems;
        private string[] _nativePackDragBefore;
        private GameStyleMode _nativePackDragStyle;
        private bool _savingNativePackOrder, _packCatalogChangedDuringDrag;

        private async void OnPackCardHostDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs e)
        {
            if (sender is ContentControl host) await EnsurePackCardHostAsync(host);
        }

        private async void OnPackCardHostLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ContentControl host) await EnsurePackCardHostAsync(host);
        }

        private void OnPackCardHostUnloaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is ContentControl host)) return;
            host.Tag = null; // Invalidates an in-flight load before releasing the visual.
            host.Content = null;
        }

        private async Task EnsurePackCardHostAsync(ContentControl host)
        {
            try
            {
                var entry = host.DataContext as PackCardEntry;
                if (host.Tag is PackCardHostLoad active && ReferenceEquals(active.Entry, entry)) return;
                host.Tag = null;
                host.Content = null;
                if (entry == null) return;
                var load = new PackCardHostLoad { Entry = entry };
                host.Tag = load;
                load.Loading = LoadPackCardHostAsync(host, load);
                await load.Loading;
            }
            catch (Exception ex) { App.Log("Pack card host failed: " + ex); }
        }

        private async Task LoadPackCardHostAsync(ContentControl host, PackCardHostLoad load)
        {
            PackCardEntry entry = load.Entry;
            try
            {
                // A visual belongs to this template instance, never to the shared data item.
                UIElement content = entry.IsVoice
                    ? await BuildVoicePackRowAsync(entry.Voice, entry.Ordinal - 1)
                    : await BuildIconPackRowAsync(entry.Icon, entry.Ordinal - 1);
                if (!ReferenceEquals(host.Tag, load) || !ReferenceEquals(host.DataContext, entry)) return;
                if (content is Border card && card.Tag is TextBlock number)
                    number.SetBinding(TextBlock.TextProperty, new Binding
                    {
                        Source = entry, Path = new PropertyPath(nameof(PackCardEntry.OrdinalText)), Mode = BindingMode.OneWay
                    });
                host.Content = content;
            }
            catch (Exception ex)
            {
                App.Log("Pack card failed: " + entry.Key + ": " + ex);
                if (!ReferenceEquals(host.Tag, load) || !ReferenceEquals(host.DataContext, entry)) return;
                host.Content = new TextBlock { Text = entry.Voice?.DisplayName ?? entry.Icon?.DisplayName,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
            }
        }

        private void OnNativePackDragStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (_packZipDropInProgress || _packOrderMoveInProgress || !_isSettingsPageLoaded
                || e.Items.Count != 1 || !(e.Items[0] is PackCardEntry)) { e.Cancel = true; return; }
            var grid = (GridView)sender;
            if (!(grid.ItemsSource is ObservableCollection<PackCardEntry> items)) { e.Cancel = true; return; }
            _nativePackDragGrid = grid;
            _nativePackDragItems = items;
            _nativePackDragBefore = items.Select(item => item.Key).ToArray();
            _nativePackDragStyle = GameStyleService.Current;
            _packOrderMoveInProgress = true;
            App.Log("Native pack drag starting: " + ((PackCardEntry)e.Items[0]).Key);
            // No DataPackage, preview, deferral or pointer handling: native GridView owns the drag.
        }

        private async void OnNativePackDragCompleted(ListViewBase sender, DragItemsCompletedEventArgs e)
        {
            if (sender != _nativePackDragGrid) return;
            var items = _nativePackDragItems;
            App.Log("Native pack drag completed: " + e.Items.Count);
            bool reload = false;
            try
            {
                if (!ReferenceEquals(sender.ItemsSource, items) || _nativePackDragStyle != GameStyleService.Current) return;
                // The native collection is authoritative, including cancellation/no-op drops.
                if (items.Select(item => item.Key).SequenceEqual(_nativePackDragBefore, StringComparer.OrdinalIgnoreCase)) return;
                if (e.Items.Count != 1 || !(e.Items[0] is PackCardEntry moved)) { reload = true; return; }
                int index = items.IndexOf(moved);
                if (index < 0 || items.Count < 2) { reload = true; return; }
                _savingNativePackOrder = true;
                string neighbour = index == 0 ? items[1].Key : items[index - 1].Key;
                int saved = await PackCatalogService.ReorderPackAsync(moved.Key, moved.IsVoice, neighbour, index > 0);
                if (saved < 0) { reload = true; return; }
                for (int i = 0; i < items.Count; i++) items[i].UpdateOrdinal(i + 1);
                App.Log("Native pack reorder: " + moved.Key + " -> " + saved);
            }
            catch (Exception ex) { reload = true; App.Log("Native pack reorder failed: " + ex); }
            finally
            {
                reload |= _packCatalogChangedDuringDrag;
                ResetNativePackDrag();
                if (reload)
                {
                    _loadedVoicePackStyle = null;
                    _loadedIconPackStyle = null;
                    await EnsureActivePackListLoadedAsync();
                }
            }
        }

        private void ResetNativePackDrag()
        {
            _nativePackDragGrid = null;
            _nativePackDragItems = null;
            _nativePackDragBefore = null;
            _packOrderMoveInProgress = false;
            _savingNativePackOrder = false;
            _packCatalogChangedDuringDrag = false;
        }

        private Border CreatePackCard(Grid row, string key, bool voice, int ordinal, string displayName)
        {
            var layers = new Grid();
            // Canvas children do not participate in measuring the card.
            var background = new Canvas { IsHitTestVisible = false, Tag = "PackDecoration" };
            var number = new TextBlock
            {
                Text = ordinal.ToString("D2"), FontSize = 52, Width = 130,
                TextAlignment = TextAlignment.Right,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(24, 75, 93, 130))
            };
            background.Children.Add(number);
            background.SizeChanged += (_, __) => Canvas.SetLeft(number, background.ActualWidth - number.Width);
            layers.Children.Add(background);
            layers.Children.Add(row);

            var grip = new Grid { Width = 9, Height = 15 };
            for (int i = 0; i < 6; i++)
            {
                var dot = new Ellipse { Width = 3, Height = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(255, 115, 124, 140)),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(i % 2 * 6, i / 2 * 6, 0, 0) };
                grip.Children.Add(dot);
            }
            var handle = new Border
            {
                Width = 28, Height = 26, Background = new SolidColorBrush(Colors.Transparent),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                Child = grip, Tag = "PackDecoration"
            };
            string hint = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "拖拽调整顺序" : "Drag to reorder";
            ToolTipService.SetToolTip(handle, hint);
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(handle, hint);
            layers.Children.Add(handle);
            var card = new Border
            {
                Width = 258, Height = 96, Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 229, 229, 229)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 8, 8), Child = layers
            };
            card.Tag = number;
            return card;
        }
    }
}
