using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        internal static void NotifyCustomSequenceSelectionChanged() => CatalogChanged?.Invoke(null, EventArgs.Empty);

        internal static async Task<IconPackItem> RegisterCustomSequencePackAsync(StorageFolder folder, string name)
        {
            var pack = new IconPackItem
            {
                Key = "custom_module_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = name, FolderPath = folder.Path,
                IsBuiltIn = false, IsVisibleInWidget = true, OwnsFolder = true
            };
            var catalog = await LoadAsync();
            catalog.IconPacks.Add(pack);
            try { await SaveAsync(catalog); }
            catch { catalog.IconPacks.Remove(pack); throw; }
            return pack;
        }
    }
}
