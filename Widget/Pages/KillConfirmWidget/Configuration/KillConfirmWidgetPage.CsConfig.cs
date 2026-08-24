using System;
using System.Collections.Generic;
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
        private const string SteamFolderName = "steam";
        private const string SteamAppsFolderName = "steamapps";
        private const string CommonFolderName = "common";
        private const string InstallRootFolderName = "Counter-Strike Global Offensive";
        private const string GameFolderName = "game";
        private const string CsgoFolderName = "csgo";
        private const string CfgFolderName = "cfg";
        private const string CsgoLegacyExecutableName = "csgo.exe";

        // Ordered path segments that lead from a picked folder down to the
        // CS2/CSGO cfg folder. Each segment only branches when the folder
        // actually exists, so the walk stays on the real install path.
        private static readonly string[] Cs2CfgResolveSpineNames =
        {
            SteamFolderName,
            "steamlibrary",
            "steam library",
            "games",
            SteamAppsFolderName,
            CommonFolderName,
            InstallRootFolderName,
            "Counter-Strike 2",
            "Counter Strike Global Offensive",
            "Counter-Strike",
            "CS2",
            GameFolderName,
        };

        // Legacy CS:GO has no game/ folder — its cfg lives at <root>/csgo/cfg.
        // Excluding "game" here keeps a CS2-only install (game/csgo) from being
        // mistaken for a legacy install when the user is in CS:GO mode.
        private static readonly string[] LegacyCfgResolveSpineNames =
        {
            SteamFolderName,
            "steamlibrary",
            "steam library",
            "games",
            SteamAppsFolderName,
            CommonFolderName,
            InstallRootFolderName,
            "Counter-Strike 2",
            "Counter Strike Global Offensive",
            "Counter-Strike",
            "CS2",
        };

        private string[] CfgResolveSpineNames => IsCsgoLegacyCfgMode
            ? LegacyCfgResolveSpineNames
            : Cs2CfgResolveSpineNames;

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
            if (_csInstallFolder == null && string.IsNullOrWhiteSpace(_serviceDetectedCsRootPath))
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
            _serviceDetectedCsRootPath = string.Empty;
            _serviceDetectedCfgStatus = string.Empty;
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
                    string cfgStatus = json.GetNamedString("cfg_status", string.Empty);

                    if (!found || string.IsNullOrWhiteSpace(path))
                    {
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                        return;
                    }

                    _serviceDetectedCsRootPath = path;
                    _serviceDetectedCfgStatus = cfgStatus;
                    ApplicationData.Current.LocalSettings.Values[CurrentCsInstallFolderPathSettingKey] = path;

                    try
                    {
                        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(path);
                        SaveCsFolder(folder);
                        await RefreshCfgStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        App.Log("Auto-detected CS folder, but folder access failed: " + ex);
                        ApplyServiceDetectedCfgStatus(path, cfgStatus);
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

            App.Log(
                "CFG folder resolved: selected=" + (_csInstallFolder.Path ?? _csInstallFolder.Name)
                + ", cfg=" + (cfgFolder.Path ?? cfgFolder.Name));

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
            if (_csInstallFolder == null && !string.IsNullOrWhiteSpace(_serviceDetectedCsRootPath))
            {
                await InstallCfgThroughServiceAsync();
                return;
            }

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

        private async Task InstallCfgThroughServiceAsync()
        {
            try
            {
                UpdateCfgStatus(CfgDetectionState.Checking, LocalizationManager.Text("CfgAdding"), _serviceDetectedCsRootPath);
                var uri = new Uri(
                    CounterStrikeCfgUri
                    + "?version="
                    + Uri.EscapeDataString(_loadedCsGameVersion));
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (HttpResponseMessage response = await client.PostAsync(uri, null))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("CFG service install failed: " + response.StatusCode);
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    _serviceDetectedCfgStatus = json.GetNamedString("cfg_status", "ready");
                    ApplyServiceDetectedCfgStatus(_serviceDetectedCsRootPath, _serviceDetectedCfgStatus);
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to install cfg through local service: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, LocalizationManager.Text("CfgAddFailed"), _serviceDetectedCsRootPath);
                await ShowCfgMessageAsync(LocalizationManager.Text("CfgWriteFailed"));
            }
        }

        private void ApplyServiceDetectedCfgStatus(string path, string cfgStatus)
        {
            switch ((cfgStatus ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "ready":
                    UpdateCfgStatus(CfgDetectionState.Ready, null, path);
                    break;
                case "outdated":
                    UpdateCfgStatus(CfgDetectionState.Outdated, null, LocalizationManager.Text("CfgOutdatedHint") + " " + path);
                    break;
                case "missing":
                    UpdateCfgStatus(CfgDetectionState.Missing, null, path);
                    break;
                default:
                    UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgDetectedNeedConfirm") + path);
                    break;
            }
        }

        private IBuffer GetExpectedCfgBuffer()
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

        private const int MaxCfgResolveDepth = 10;

        // Resolve the CS2/CSGO cfg folder no matter which level of the install
        // path the user picked (steam, steamapps, common, the game root, game,
        // csgo, or cfg itself). The widget only has access to the picked folder's
        // subtree, so the search walks downward through known path segments and
        // never tries to reach a parent folder.
        private async Task<StorageFolder> TryGetCfgFolderAsync(StorageFolder root)
        {
            if (string.Equals(root.Name, CfgFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            StorageFolder csgo = await TryResolveCsgoFolderAsync(root, 0);
            if (csgo == null)
            {
                return null;
            }
            return await TryGetSubfolderAsync(csgo, CfgFolderName);
        }

        private async Task<StorageFolder> GetOrCreateCfgFolderAsync(StorageFolder root)
        {
            if (string.Equals(root.Name, CfgFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            StorageFolder csgo = await TryResolveCsgoFolderAsync(root, 0);
            if (csgo == null)
            {
                return null;
            }
            return await csgo.CreateFolderAsync(CfgFolderName, CreationCollisionOption.OpenIfExists);
        }

        // Find the csgo folder below the picked folder. The folder names on the
        // path are matched case-insensitively; each segment only branches when it
        // actually exists, so this is a short walk along the real install path
        // rather than a directory scan.
        // Helper to retrieve a chain of nested subfolders (e.g. game -> csgo).
        private async Task<StorageFolder> TryGetSubfolderChainAsync(StorageFolder folder, params string[] names)
        {
            if (folder == null || names == null || names.Length == 0)
            {
                return null;
            }

            StorageFolder current = folder;
            foreach (string name in names)
            {
                current = await TryGetSubfolderAsync(current, name);
                if (current == null)
                {
                    return null;
                }
            }
            return current;
        }
    }
}
