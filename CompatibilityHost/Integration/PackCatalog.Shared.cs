using System.Threading.Tasks;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        internal static async Task ReloadSharedCatalogAsync()
        {
            await CatalogIoLock.WaitAsync();
            try { _cache = null; }
            finally { CatalogIoLock.Release(); }
            await LoadAsync();
        }
    }
}
