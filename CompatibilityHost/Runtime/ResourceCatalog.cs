using System.Threading.Tasks;

namespace KillConfirmCompatibility.Services
{
    public static partial class PackCatalogService
    {
        internal static async Task ReloadForCompatibilityAsync()
        {
            await CatalogIoLock.WaitAsync();
            try { _cache = null; }
            finally { CatalogIoLock.Release(); }
            await LoadAsync();
        }
    }
}
