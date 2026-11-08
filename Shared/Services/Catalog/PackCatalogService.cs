// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.AccessCache;
#if WINDOWS_UWP
using KillConfirmGameBar.Helpers;
#else
using KillConfirmCompatibility.Helpers;
#endif

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    public static partial class PackCatalogService
    {
        private const string CatalogFileName = "pack-catalog.json";
        private const string VisibilityDefaultsVersionKey = "PackCatalogVisibilityDefaultsVersion";
        private const int CurrentVisibilityDefaultsVersion = 12;
        private const string DefaultVoiceKey = "crossfire_swat_gr";
        private const string DefaultIconKey = "default";
        private static readonly string[] SupportedAudioExtensions = { ".wav", ".mp3", ".m4a" };
        private static readonly string[] IconImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".tga" };
        private static readonly SemaphoreSlim CatalogIoLock = new SemaphoreSlim(1, 1);
        private static PackCatalog _cache;

        public static event EventHandler CatalogChanged;
#if WINDOWS_UWP
        internal static void ResetSharedCache() { _cache=null; CatalogChanged?.Invoke(null,EventArgs.Empty); }
#endif
    }
}
