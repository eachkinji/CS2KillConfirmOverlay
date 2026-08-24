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

        // Find the csgo folder below or at the picked folder. The folder names on the
        // path are matched case-insensitively; each segment only branches when it
        // actually exists, so this is a short walk along the real install path.
        private async Task<StorageFolder> TryResolveCsgoFolderAsync(StorageFolder folder, int depth)
        {
            if (folder == null || depth > MaxCfgResolveDepth)
            {
                return null;
            }

            // A directly selected csgo folder is already the version-specific
            // game data folder. Its parent is outside the UWP access grant, so
            // accept it without attempting to infer the version from ancestors.
            if (string.Equals(folder.Name, CsgoFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return folder;
            }

            // Resolve an install root using only the layout for the selected
            // game version. This prevents CS2's game/csgo/cfg and Legacy's
            // csgo/cfg from being mistaken for one another.
            StorageFolder versionSpecificCsgo =
                await TryCsgoSubfolderOfInstallRootAsync(folder);
            if (versionSpecificCsgo != null)
            {
                return versionSpecificCsgo;
            }

            // Walk downward only through folders that can lead to an install
            // root. The terminal csgo segment is deliberately excluded so it
            // cannot bypass the version-specific layout check above.
            foreach (string name in CfgResolveSpineNames)
            {
                StorageFolder child = await TryGetSubfolderAsync(folder, name);
                if (child != null)
                {
                    StorageFolder result = await TryResolveCsgoFolderAsync(child, depth + 1);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            // 5. Fallback scan for library level (e.g. common / steamapps / custom library root)
            if (depth <= 3)
            {
                List<StorageFolder> subfolders = await TryListSubfoldersAsync(folder, 100);
                foreach (StorageFolder child in subfolders)
                {
                    StorageFolder fromChild =
                        await TryCsgoSubfolderOfInstallRootAsync(child);
                    if (fromChild != null)
                    {
                        return fromChild;
                    }
                }
            }

            return null;
        }

        // Return the csgo subfolder when folder is the install root of the current
        // game version: CS2 is <root>/game/csgo, legacy CSGO is <root>/csgo.
        private async Task<StorageFolder> TryCsgoSubfolderOfInstallRootAsync(StorageFolder folder)
        {
            if (folder == null)
            {
                return null;
            }

            try
            {
                if (IsCsgoLegacyCfgMode)
                {
                    StorageFile executable =
                        await TryGetFileAsync(folder, CsgoLegacyExecutableName);
                    if (executable == null)
                    {
                        return null;
                    }

                    return await TryGetSubfolderAsync(folder, CsgoFolderName);
                }

                if (string.Equals(folder.Name, GameFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    return await TryGetSubfolderAsync(folder, CsgoFolderName);
                }

                return await TryGetSubfolderChainAsync(folder, GameFolderName, CsgoFolderName);
            }
            catch
            {
                return null;
            }
        }

        private async Task<StorageFolder> TryGetSubfolderAsync(StorageFolder folder, string name)
        {
            try
            {
                return await folder.GetFolderAsync(name);
            }
            catch
            {
                return null;
            }
        }

        private async Task<StorageFile> TryGetFileAsync(StorageFolder folder, string name)
        {
            try
            {
                return await folder.GetFileAsync(name);
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<StorageFolder>> TryListSubfoldersAsync(StorageFolder folder, int limit)
        {
            try
            {
                var folders = new List<StorageFolder>();
                foreach (StorageFolder child in await folder.GetFoldersAsync())
                {
                    folders.Add(child);
                    if (folders.Count >= limit)
                    {
                        break;
                    }
                }
                return folders;
            }
            catch
            {
                return new List<StorageFolder>();
            }
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
