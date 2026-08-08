using System.Collections.Generic;
using System.Runtime.Serialization;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    [DataContract]
    public sealed class PackCatalog
    {
        [DataMember]
        public List<VoicePackItem> VoicePacks { get; set; } = new List<VoicePackItem>();

        [DataMember]
        public List<IconPackItem> IconPacks { get; set; } = new List<IconPackItem>();
    }

    [DataContract]
    public sealed class VoicePackItem
    {
        [DataMember]
        public string Key { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        [DataMember]
        public string FolderPath { get; set; }

        [DataMember]
        public bool IsBuiltIn { get; set; }

        [DataMember]
        public bool IsVisibleInWidget { get; set; }

        [DataMember]
        public bool OwnsFolder { get; set; }
    }

    [DataContract]
    public sealed class IconPackItem
    {
        [DataMember]
        public string Key { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        [DataMember]
        public string FolderPath { get; set; }

        [DataMember]
        public string FolderToken { get; set; }

        [DataMember]
        public bool IsBuiltIn { get; set; }

        [DataMember]
        public bool IsVisibleInWidget { get; set; }

        [DataMember]
        public bool OwnsFolder { get; set; }

        [DataMember]
        public bool HasFxOverlay { get; set; }

        [DataMember]
        public bool HasKillFxOverlay { get; set; }

        [DataMember]
        public bool HasEliteOverlay { get; set; }

        [DataMember]
        public bool HasWeaponBadgeOverlay { get; set; }
    }

    public sealed class IconPackCapabilities
    {
        public bool HasKillFxOverlay { get; set; }
        public bool HasEliteOverlay { get; set; }
        public bool HasWeaponBadgeOverlay { get; set; }
    }

    public sealed class VoicePackBuildOptions
    {
        public IReadOnlyDictionary<string, StorageFile> SelectedFiles { get; set; }
        public IReadOnlyDictionary<string, bool> CommonOverlayEnabled { get; set; }
        public bool UseBuiltInDefaultCommonOverlay { get; set; }
        public StorageFile CommonOverlayFile { get; set; }
        public StorageFile HeadImageFile { get; set; }
    }
}
