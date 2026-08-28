using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private async Task ImportCustomModuleAsync(bool zip)
        {
            IconPackItem imported = null;
            try
            {
                Func<IProgress<string>, Task<IconPackItem>> import;
                if (zip)
                {
                    var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".zip");
                    StorageFile file = await picker.PickSingleFileAsync();
                    if (file == null) return;
                    import = progress => CustomSequencePackService.ImportZipAsync(file, progress);
                }
                else
                {
                    var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
                    StorageFolder folder = await picker.PickSingleFolderAsync();
                    if (folder == null) return;
                    import = progress => CustomSequencePackService.ImportFolderAsync(folder, progress);
                }
                var progressText = new TextBlock { Text = "Import / 导入…", TextWrapping = TextWrapping.Wrap };
                var dialog = new ContentDialog { Title = "Custom Module / 自定义模块", Content = progressText };
                bool running = true;
                dialog.Closing += (s, e) => e.Cancel = running;
                var showing = dialog.ShowAsync();
                try { imported = await import(new Progress<string>(text => progressText.Text = text)); }
                finally { running = false; dialog.Hide(); await showing; }
                if (imported != null)
                {
                    ApplicationData.Current.LocalSettings.Values["KillIconPack.custommodule"] = imported.Key;
                    PackCatalogService.NotifyCustomSequenceSelectionChanged();
                    await ShowMessageAsync("Custom Module / 自定义模块", imported.DisplayName + "\nImport complete / 导入完成。高级设置中可预览、调节和导出。");
                }
            }
            catch (Exception ex) { await ShowMessageAsync("Custom Module / 自定义模块", ex.Message); }
        }

        private async Task ShowCustomModuleEditorAsync(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                ApplicationData.Current.LocalSettings.Values["KillIconPack.custommodule"] = key;
                PackCatalogService.NotifyCustomSequenceSelectionChanged();
            }
            var panel = new CustomModulePanel();
            panel.ApplyLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
            panel.ApplyTheme(GameThemePalette.ForMode(GameStyleMode.CustomModule));
            var dialog = new ContentDialog
            {
                Title = "Custom Module / 自定义模块",
                Content = new ScrollViewer { Content = panel, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                CloseButtonText = "Close / 关闭"
            };
            panel.AllowDelete = false; // A ContentDialog cannot open another ContentDialog.
            await dialog.ShowAsync();
        }
    }
}
