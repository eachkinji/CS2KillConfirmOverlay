using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmCompatibility.Services
{
    /// <summary>
    /// Discovers and installs split VALORANT resource packages. Icon and voice
    /// packages have independent ids and share association_id.
    /// </summary>
    internal static partial class ValorantExternalAssetService
    {
        [DataContract]
        private sealed class ValorantExternalPackManifest
        {
            [DataMember(Name = "format_version")]
            public int FormatVersion { get; set; }
            [DataMember(Name = "package_kind")]
            public string PackageKind { get; set; }
            [DataMember(Name = "id")]
            public string Id { get; set; }
            [DataMember(Name = "association_id")]
            public string AssociationId { get; set; }
            [DataMember(Name = "display_name")]
            public string DisplayName { get; set; }
            [DataMember(Name = "display_name_zh_cn")]
            public string DisplayNameZhCn { get; set; }
            [DataMember(Name = "game_style")]
            public string GameStyle { get; set; }
            [DataMember(Name = "profile")]
            public ValorantExternalProfileManifest Profile { get; set; }
        }

        [DataContract]
        private sealed class ValorantExternalProfileManifest
        {
            [DataMember(Name = "accent")]
            public string Accent { get; set; }
            [DataMember(Name = "emblem")]
            public string Emblem { get; set; }
            [DataMember(Name = "frame")]
            public string Frame { get; set; }
            [DataMember(Name = "bar")]
            public string Bar { get; set; }
            [DataMember(Name = "bar_hover")]
            public string BarHover { get; set; }
            [DataMember(Name = "ring")]
            public string Ring { get; set; }
            [DataMember(Name = "frame_dissolve")]
            public string FrameDissolve { get; set; }
            [DataMember(Name = "badge_dissolve")]
            public string BadgeDissolve { get; set; }
            [DataMember(Name = "blade")]
            public string Blade { get; set; }
            [DataMember(Name = "special_frame")]
            public string SpecialFrame { get; set; }
            [DataMember(Name = "headshot_x")]
            public double HeadshotX { get; set; }
            [DataMember(Name = "headshot_y")]
            public double HeadshotY { get; set; }
            [DataMember(Name = "slice_size")]
            public double SliceSize { get; set; }
        }
    }

    internal sealed class ValorantPackageInstallResult
    {
        public string Id { get; set; }
        public string AssociationId { get; set; }
        public string DisplayName { get; set; }
        public string PackageKind { get; set; }    }
}
