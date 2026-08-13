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
using KillConfirmGameBar.Helpers;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        private const string CatalogFileName = "pack-catalog.json";
        private const string VisibilityDefaultsVersionKey = "PackCatalogVisibilityDefaultsVersion";
        private const int CurrentVisibilityDefaultsVersion = 9;
        private const string DefaultVoiceKey = "crossfire_swat_gr";
        private const string DefaultIconKey = "default";
        private static readonly string[] SupportedAudioExtensions = { ".wav", ".mp3", ".m4a" };
        private static readonly string[] IconImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".tga" };
        private static readonly SemaphoreSlim CatalogIoLock = new SemaphoreSlim(1, 1);
        private static PackCatalog _cache;

        public static event EventHandler CatalogChanged;
    }
}
