using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        internal static async Task<IconPackItem> SaveCustomSequencePackAsync(StorageFolder folder, string name, string existingKey = null)
        {
            var catalog = await LoadAsync();
            var packRoot = await GetGameIconPacksFolderAsync("custommodule");
            if (!string.Equals(Path.GetDirectoryName(folder.Path), packRoot.Path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Custom pack must be staged in the icon library.");
            IconPackItem previous = null;
            IconPackItem pack = null;
            await CatalogIoLock.WaitAsync();
            try
            {
                previous = existingKey == null ? null : catalog.IconPacks.FirstOrDefault(p => p.Key == existingKey);
                if (existingKey != null && (previous == null || previous.IsBuiltIn || !previous.OwnsFolder
                    || !existingKey.StartsWith("custom_module_icon_", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Custom pack no longer exists / 自定义包已不存在，请刷新图标包库。");
                pack = new IconPackItem
                {
                    Key = previous?.Key ?? "custom_module_icon_" + Guid.NewGuid().ToString("N"),
                    DisplayName = name, FolderPath = folder.Path,
                    IsBuiltIn = false, IsVisibleInWidget = previous?.IsVisibleInWidget ?? true, OwnsFolder = true
                };
                int index = previous == null ? catalog.IconPacks.Count : catalog.IconPacks.IndexOf(previous);
                if (previous == null) catalog.IconPacks.Add(pack); else catalog.IconPacks[index] = pack;
                StorageFile temporary = null;
                try
                {
                    var local = ApplicationData.Current.LocalFolder;
                    temporary = await local.CreateFileAsync("custom-catalog-" + Guid.NewGuid().ToString("N") + ".tmp");
                    using (var stream = await temporary.OpenStreamForWriteAsync())
                    {
                        new DataContractJsonSerializer(typeof(PackCatalog)).WriteObject(stream, catalog);
                        await stream.FlushAsync();
                    }
                    var current = await local.TryGetItemAsync(CatalogFileName) as StorageFile;
                    if (current == null) await temporary.MoveAsync(local, CatalogFileName, NameCollisionOption.FailIfExists);
                    else await temporary.MoveAndReplaceAsync(current);
                    temporary = null;
                }
                catch
                {
                    if (previous == null) catalog.IconPacks.Remove(pack); else catalog.IconPacks[index] = previous;
                    throw;
                }
                finally { if (temporary != null) await temporary.DeleteAsync(); }
            }
            finally { CatalogIoLock.Release(); }
            // Notification failures must not roll back a successfully persisted pack.
            try { CatalogChanged?.Invoke(null, EventArgs.Empty); }
            catch (Exception ex) { App.Log("Custom pack catalog notification: " + ex); }
            if (previous != null && string.Equals(Path.GetDirectoryName(previous.FolderPath), packRoot.Path, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(previous.FolderPath, folder.Path, StringComparison.OrdinalIgnoreCase))
            {
                try { await (await StorageFolder.GetFolderFromPathAsync(previous.FolderPath)).DeleteAsync(StorageDeleteOption.PermanentDelete); }
                catch (Exception ex) { App.Log("Custom pack old copy cleanup: " + ex); }
            }
            return pack;
        }
    }
}
