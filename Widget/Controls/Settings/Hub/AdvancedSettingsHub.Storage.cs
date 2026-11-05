using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class AdvancedSettingsHub
    {
        private void RefreshMaterialLibrary()
        {
            HubMaterialPath.Text = ResourceLibraryPaths.Root;
#if DESKTOP_CONTROL_PANEL
            HubInstallPath.Text = KillConfirmCompatibility.Contracts.RuntimePaths.InstallRoot.TrimEnd('\\');
#else
            HubInstallPath.Text = "在控制面板查看程序安装位置";
#endif
#if SHARED_WIDGET_RUNTIME
            HubMaterialPath.Text = SharedResources.RemoteLibraryRoot ?? "请在控制面板查看素材目录";
            HubInstallPath.Text = SharedResources.RemoteInstallRoot ?? "请在控制面板查看程序安装位置";
            HubMaterialMove.Content = "在控制面板管理";
#endif
        }
        private void SetStorageStatus(string message)
        {
            HubMaterialStatus.Text = message;
            HubMaterialStatus.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }
        private async void OnOpenInstallFolder(object sender, RoutedEventArgs e)
        {
#if DESKTOP_CONTROL_PANEL
            try { await Windows.System.Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(KillConfirmCompatibility.Contracts.RuntimePaths.InstallRoot)); }
            catch (Exception error) { SetStorageStatus(error.Message); }
#else
            await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("OpenSettingsWindow");
#endif
        }
        private async void OnOpenMaterialLibrary(object sender, RoutedEventArgs e)
        {
#if SHARED_WIDGET_RUNTIME
            await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("OpenSettingsWindow");
#else
            try { await Windows.System.Launcher.LaunchFolderAsync(await ResourceLibraryPaths.FolderAsync()); }
            catch (Exception error) { SetStorageStatus(error.Message); }
#endif
        }
        private async void OnMoveMaterialLibrary(object sender, RoutedEventArgs e)
        {
#if DESKTOP_CONTROL_PANEL
            HubMaterialMove.IsEnabled = false;
            try
            {
                string destination = await ChooseMaterialDestinationAsync();
                if (destination == null) return;
                var confirmation = new ContentDialog { XamlRoot = XamlRoot, Title = "迁移素材包",
                    Content = "当前位置：\n" + ResourceLibraryPaths.Root + "\n\n新位置：\n" + destination + "\n\n现有设置会保留，素材路径会自动更新。复制并校验成功后才清理旧素材；完成后重新打开控制面板。",
                    PrimaryButtonText = "迁移", CloseButtonText = "取消" };
                if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;
                await RuntimeSession.StopAsync();
                SetStorageStatus("正在复制并校验素材，请勿关闭控制面板…");
                var progress = new Progress<string>(SetStorageStatus);
                await Task.Run(() => KillConfirmCompatibility.Contracts.ResourceLibrary.Migrate(
                    KillConfirmCompatibility.Contracts.RuntimePaths.DataRoot, destination, text => ((IProgress<string>)progress).Report(text)));
                RestartAfterMaterialMove();
            }
            catch (Exception error)
            {
                SetStorageStatus("迁移未完成：" + error.Message);
                RuntimeSession.Resume();
                RefreshMaterialLibrary();
            }
            finally { HubMaterialMove.IsEnabled = true; }
#else
            await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("OpenSettingsWindow");
#endif
        }
#if DESKTOP_CONTROL_PANEL
        private async Task<string> ChooseMaterialDestinationAsync()
        {
            var inputStyle = (Style)Resources["FluentInputStyle"];
            var buttonStyle = (Style)Resources["FluentSecondaryButtonStyle"];
            var parent = new TextBox { Header = "存放位置", Text = Path.GetDirectoryName(ResourceLibraryPaths.Root.TrimEnd('\\')), Style = inputStyle };
            var name = new TextBox { Header = "下级文件夹名称", Text = "KillConfirm素材包", Style = inputStyle };
            var target = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontSize = 12 };
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
            var browse = new Button { Content = "选择位置…", Style = buttonStyle };
            var create = new Button { Content = "新建文件夹", Style = buttonStyle };
            string Destination()
            {
                string leaf = name.Text.Trim();
                if (leaf.Length == 0 || leaf == "." || leaf == ".." || leaf.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || leaf.EndsWith("."))
                    throw new IOException("请输入有效的下级文件夹名称。");
                string location = parent.Text.Trim();
                if (!Path.IsPathFullyQualified(location)) throw new IOException("请选择完整的存放路径。");
                return Path.GetFullPath(Path.Combine(location, leaf));
            }
            void Refresh()
            {
                try { target.Text = "最终存放路径：\n" + Destination(); status.Text = ""; }
                catch (Exception error) { target.Text = ""; status.Text = error.Message; }
            }
            parent.TextChanged += (s, e) => Refresh(); name.TextChanged += (s, e) => Refresh();
            browse.Click += async (s, e) =>
            {
                var picker = new FolderPicker { CommitButtonText = "选择存放位置" };
                picker.FileTypeFilter.Add("*");
                try
                {
                    var folder = await picker.PickSingleFolderAsync();
                    if (folder == null) return;
                    string selected = folder.Path.TrimEnd('\\');
                    parent.Text = string.Equals(Path.GetFileName(selected), name.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                        ? Path.GetDirectoryName(selected) : folder.Path;
                }
                catch (Exception error) { status.Text = error.Message; }
            };
            create.Click += (s, e) =>
            {
                try { Directory.CreateDirectory(Destination()); status.Text = "文件夹已就绪，点击下一步确认迁移。"; }
                catch (Exception error) { status.Text = error.Message; }
            };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            buttons.Children.Add(browse); buttons.Children.Add(create);
            var content = new StackPanel { Spacing = 12, MinWidth = 340 };
            content.Children.Add(new TextBlock { Text = "选择磁盘或父文件夹后，会自动使用下面的下级文件夹。", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            content.Children.Add(parent); content.Children.Add(name); content.Children.Add(buttons); content.Children.Add(target); content.Children.Add(status);
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "选择素材存放位置", Content = content, PrimaryButtonText = "下一步", CloseButtonText = "取消" };
            string result = null;
            dialog.PrimaryButtonClick += (s, e) =>
            {
                try
                {
                    result = Destination();
                    if (!string.Equals(result.TrimEnd('\\'), ResourceLibraryPaths.Root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                        && Directory.Exists(result) && Directory.EnumerateFileSystemEntries(result).Any())
                        throw new IOException("目标文件夹已有文件，请选择空文件夹或更换下级文件夹名称。");
                }
                catch (Exception error) { status.Text = error.Message; e.Cancel = true; }
            };
            Refresh();
            return await dialog.ShowAsync() == ContentDialogResult.Primary ? result : null;
        }
        private void RestartAfterMaterialMove()
        {
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false };
            start.ArgumentList.Add("--wait-for-pid");
            start.ArgumentList.Add(Environment.ProcessId.ToString());
            System.Diagnostics.Process.Start(start)?.Dispose();
            Application.Current.Exit();
        }
#endif
    }
}
