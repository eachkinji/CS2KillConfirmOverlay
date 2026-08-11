using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private bool IsCsgoLegacyCfgMode =>
            string.Equals(
                _loadedCsGameVersion,
                GsiGameVersionSettingsStore.CsgoLegacy,
                StringComparison.Ordinal);

        private string CurrentCsInstallFolderAccessToken => IsCsgoLegacyCfgMode
            ? CsgoLegacyInstallFolderAccessToken
            : Cs2InstallFolderAccessToken;

        private string CurrentCsInstallFolderTokenSettingKey => IsCsgoLegacyCfgMode
            ? CsgoLegacyInstallFolderTokenSettingKey
            : Cs2InstallFolderTokenSettingKey;

        private string CurrentCsInstallFolderPathSettingKey => IsCsgoLegacyCfgMode
            ? CsgoLegacyInstallFolderPathSettingKey
            : Cs2InstallFolderPathSettingKey;

        private void OnGsiGameVersionChanged(object sender, EventArgs e)
        {
            _ = Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => { _ = LoadSavedCsFolderAsync(); });
        }

        private async void OnSelectCsFolderClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add("*");

            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            try
            {
                SaveCsFolder(folder);
                await RefreshCfgStatusAsync();
            }
            catch (Exception ex)
            {
                App.Log("Failed to save selected CS folder: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, LocalizationManager.Text("CfgFolderError"), LocalizationManager.Text("CfgFolderSaveError"));
            }
        }

        private async void OnInstallCfgClick(object sender, RoutedEventArgs e)
        {
            if (_csInstallFolder == null)
            {
                await ShowCfgMessageAsync(LocalizationManager.Text("SelectCsFirst"));
                return;
            }

            bool isRequiredUpdate = _cfgDetectionState == CfgDetectionState.Outdated;
            var dialog = new MessageDialog(
                LocalizationManager.Text(isRequiredUpdate ? "UpdateCfgQuestion" : "AddCfgQuestion"),
                LocalizationManager.Text(isRequiredUpdate ? "UpdateCfgTitle" : "AddCfgTitle"));
            string addText = LocalizationManager.Text(isRequiredUpdate ? "UpdateCfgAction" : "Add");
            dialog.Commands.Add(new UICommand(addText));
            dialog.Commands.Add(new UICommand(LocalizationManager.Text("Cancel")));
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;

            IUICommand result = await dialog.ShowAsync();
            if (result.Label != addText)
            {
                return;
            }

            await InstallCfgAsync();
        }

        private async Task LoadSavedCsFolderAsync()
        {
            _loadedCsGameVersion = GsiGameVersionSettingsStore.Load();
            _csInstallFolder = null;
            string token = ApplicationData.Current.LocalSettings.Values[CurrentCsInstallFolderTokenSettingKey] as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                await TryAutoDetectCsFolderAsync();
                return;
            }

            try
            {
                _csInstallFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
                await RefreshCfgStatusAsync();
            }
            catch (Exception ex)
            {
                App.Log("Failed to restore CS folder access: " + ex);
                _csInstallFolder = null;
                await TryAutoDetectCsFolderAsync();
            }
        }

        private async Task TryAutoDetectCsFolderAsync()
        {
            if (_csInstallFolder != null)
            {
                return;
            }

            UpdateCfgStatus(CfgDetectionState.Checking, LocalizationManager.Text("CfgAutoDetecting"), LocalizationManager.Text("CfgSelectRootHint"));

            try
            {
                await EnsureServiceAvailableAsync();

                if (_serviceConnectionState != KillEventConnectionState.Connected)
                {
                    UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgDetectServiceUnavailable"));
                    return;
                }

                var rootUri = new Uri(
                    CounterStrikeRootUri
                    + "?version="
                    + Uri.EscapeDataString(_loadedCsGameVersion));
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (HttpResponseMessage response = await client.GetAsync(rootUri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgDetectServiceUnavailable"));
                        return;
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    bool found = json.GetNamedBoolean("found", false);
                    string path = json.GetNamedString("path", string.Empty);

                    if (!found || string.IsNullOrWhiteSpace(path))
                    {
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                        return;
                    }

                    try
                    {
                        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(path);
                        SaveCsFolder(folder);
                        await RefreshCfgStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        App.Log("Auto-detected CS folder, but folder access failed: " + ex);
                        ApplicationData.Current.LocalSettings.Values[CurrentCsInstallFolderPathSettingKey] = path;
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgDetectedNeedConfirm") + path);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to auto-detect CS folder: " + ex);
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgDetectServiceUnavailable"));
            }
        }

        private void SaveCsFolder(StorageFolder folder)
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(CurrentCsInstallFolderAccessToken, folder);
            ApplicationData.Current.LocalSettings.Values[CurrentCsInstallFolderTokenSettingKey] = CurrentCsInstallFolderAccessToken;
            ApplicationData.Current.LocalSettings.Values[CurrentCsInstallFolderPathSettingKey] = folder.Path;
            _csInstallFolder = folder;
        }

        private async Task RefreshCfgStatusAsync()
        {
            if (_csInstallFolder == null)
            {
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                return;
            }

            UpdateCfgStatus(CfgDetectionState.Checking, null, GetCsFolderDisplayText());

            StorageFolder cfgFolder = await TryGetCfgFolderAsync(_csInstallFolder);
            if (cfgFolder == null)
            {
                UpdateCfgStatus(CfgDetectionState.Error, null, LocalizationManager.Text("CfgWrongFolderHint"));
                return;
            }

            try
            {
                StorageFile cfgFile = await cfgFolder.GetFileAsync(GsiConfigFileName);
                string actualMd5 = ComputeCfgMd5(await FileIO.ReadTextAsync(cfgFile));
                string expectedMd5 = ComputeCfgMd5(GsiConfigText);
                if (!string.Equals(actualMd5, expectedMd5, StringComparison.OrdinalIgnoreCase))
                {
                    App.Log("CFG MD5 mismatch: expected=" + expectedMd5 + ", actual=" + actualMd5);
                    UpdateCfgStatus(CfgDetectionState.Outdated, null, LocalizationManager.Text("CfgOutdatedHint"));
                    return;
                }

                UpdateCfgStatus(CfgDetectionState.Ready, null, GetCsFolderDisplayText());
            }
            catch (System.IO.FileNotFoundException)
            {
                UpdateCfgStatus(CfgDetectionState.Missing, null, GetCsFolderDisplayText());
            }
            catch (Exception ex)
            {
                App.Log("Failed to check cfg file: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, null, GetCsFolderDisplayText());
            }
        }

        private async Task InstallCfgAsync()
        {
            try
            {
                UpdateCfgStatus(CfgDetectionState.Checking, LocalizationManager.Text("CfgAdding"), GetCsFolderDisplayText());
                StorageFolder cfgFolder = await GetOrCreateCfgFolderAsync(_csInstallFolder);
                StorageFile cfgFile = await cfgFolder.CreateFileAsync(GsiConfigFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBufferAsync(cfgFile, GetExpectedCfgBuffer());

                string installedMd5 = ComputeCfgMd5(await FileIO.ReadTextAsync(cfgFile));
                string expectedMd5 = ComputeCfgMd5(GsiConfigText);
                if (!string.Equals(installedMd5, expectedMd5, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("CFG MD5 verification failed after writing.");
                }

                App.Log("CFG installed and MD5 verified: " + installedMd5);
                UpdateCfgStatus(CfgDetectionState.Ready, null, GetCsFolderDisplayText());
            }
            catch (Exception ex)
            {
                App.Log("Failed to install cfg file: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, LocalizationManager.Text("CfgAddFailed"), GetCsFolderDisplayText());
                await ShowCfgMessageAsync(LocalizationManager.Text("CfgWriteFailed"));
            }
        }

        private static IBuffer GetExpectedCfgBuffer()
        {
            return CryptographicBuffer.ConvertStringToBinary(GsiConfigText, BinaryStringEncoding.Utf8);
        }

        private static string ComputeCfgMd5(string configText)
        {
            string normalized = (configText ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "\r\n");
            IBuffer buffer = CryptographicBuffer.ConvertStringToBinary(normalized, BinaryStringEncoding.Utf8);
            HashAlgorithmProvider provider = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
            return CryptographicBuffer.EncodeToHexString(provider.HashData(buffer)).ToUpperInvariant();
        }

        private string GetCsFolderDisplayText()
        {
            string savedPath = ApplicationData.Current.LocalSettings.Values[CurrentCsInstallFolderPathSettingKey] as string;
            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                return savedPath;
            }

            return _csInstallFolder?.Path ?? _csInstallFolder?.Name ?? "Counter-Strike Global Offensive";
        }

        private async Task<StorageFolder> TryGetCfgFolderAsync(StorageFolder root)
        {
            try
            {
                StorageFolder csgoFolder;
                if (IsCsgoLegacyCfgMode)
                {
                    await root.GetFileAsync("csgo.exe");
                    csgoFolder = await root.GetFolderAsync("csgo");
                }
                else
                {
                    StorageFolder gameFolder = await root.GetFolderAsync("game");
                    csgoFolder = await gameFolder.GetFolderAsync("csgo");
                }
                return await csgoFolder.GetFolderAsync("cfg");
            }
            catch
            {
                return null;
            }
        }

        private async Task<StorageFolder> GetOrCreateCfgFolderAsync(StorageFolder root)
        {
            StorageFolder csgoFolder;
            if (IsCsgoLegacyCfgMode)
            {
                await root.GetFileAsync("csgo.exe");
                csgoFolder = await root.GetFolderAsync("csgo");
            }
            else
            {
                StorageFolder gameFolder = await root.GetFolderAsync("game");
                csgoFolder = await gameFolder.GetFolderAsync("csgo");
            }
            return await csgoFolder.CreateFolderAsync("cfg", CreationCollisionOption.OpenIfExists);
        }

        private async Task ShowCfgMessageAsync(string message)
        {
            try
            {
                await new MessageDialog(message, LocalizationManager.Text("CfgMessageTitle")).ShowAsync();
            }
            catch
            {
            }
        }
    }
}
