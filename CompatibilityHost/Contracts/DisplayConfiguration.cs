using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace KillConfirmCompatibility.Contracts
{
    [DataContract]
    public sealed class DisplayConfiguration
    {
        [DataMember] public int Version { get; set; } = 1;
        // First use starts in Game Bar; an explicitly saved mode remains selected.
        [DataMember] public bool Enabled { get; set; } = false;
        [DataMember] public bool FollowGame { get; set; } = true;
        [DataMember] public bool HideWhenInactive { get; set; } = true;
        [DataMember] public string ScreenName { get; set; } = "";
        [DataMember] public int FramesPerSecond { get; set; } = 60;
        [DataMember] public long EditRequest { get; set; }
        [DataMember] public long TestRequest { get; set; }
        [DataMember] public long RestartRequest { get; set; }
        [DataMember] public long AssetRevision { get; set; }
        [DataMember] public long ModeRequest { get; set; }
        [DataMember] public string TestPreset { get; set; } = "three";
        [DataMember] public bool TestAudio { get; set; }
        [DataMember] public Dictionary<string, LayoutProfile> Layouts { get; set; } = new Dictionary<string, LayoutProfile>();
        [OnDeserializing]
        private void SetDefaults(StreamingContext context)
        {
            Version = 1; FollowGame = true; HideWhenInactive = true; FramesPerSecond = 60; ScreenName = "";
        }
        public LayoutProfile GetLayout(string style)
        {
            if (Layouts == null) Layouts = new Dictionary<string, LayoutProfile>();
            if (!Layouts.TryGetValue(style, out LayoutProfile layout) || layout == null)
            {
                layout = new LayoutProfile();
                layout.Crosshair.Visible = style != "crossfire" && style != "custommodule" && style != "pubg" && style != "csol" && style != "valorant";
                if (style == "overwatch") layout.Crosshair.Scale = 0.6;
                Layouts[style] = layout;
            }
            layout.Normalize();
            return layout;
        }
        public void Normalize()
        {
            FramesPerSecond = FramesPerSecond <= 30 ? 30 : 60;
            ScreenName = ScreenName ?? "";
            if (Layouts == null) Layouts = new Dictionary<string, LayoutProfile>();
            foreach (LayoutProfile profile in Layouts.Values) profile?.Normalize();
        }
    }
    [DataContract]
    public sealed class LayoutProfile
    {
        [DataMember] public ElementLayout Crosshair { get; set; } = new ElementLayout { X = 0.5, Y = 0.5 };
        [DataMember] public ElementLayout Lower { get; set; } = new ElementLayout { X = 0.5, Y = 0.8 };
        [DataMember] public ElementLayout Upper { get; set; } = new ElementLayout { X = 0.5, Y = 0.2 };
        [DataMember] public ElementLayout Danmaku { get; set; } = new ElementLayout { X = 0.5, Y = 0.2 };
        public ElementLayout GetElement(string key) => key == "Crosshair" ? Crosshair : key == "Upper" ? Upper : key == "Danmaku" ? Danmaku : Lower;
        public void Normalize()
        {
            if (Crosshair == null) Crosshair = new ElementLayout { X = 0.5, Y = 0.5 };
            if (Lower == null) Lower = new ElementLayout { X = 0.5, Y = 0.8 };
            if (Upper == null) Upper = new ElementLayout { X = 0.5, Y = 0.2 };
            if (Danmaku == null) Danmaku = new ElementLayout { X = 0.5, Y = 0.2 };
            Crosshair.Normalize(); Lower.Normalize(); Upper.Normalize(); Danmaku.Normalize();
        }
    }
    [DataContract]
    public sealed class ElementLayout
    {
        [DataMember] public double X { get; set; } = 0.5;
        [DataMember] public double Y { get; set; } = 0.5;
        [DataMember] public double Scale { get; set; } = 1;
        [DataMember] public bool Visible { get; set; } = true;
        public void Normalize()
        {
            X = Clamp(X, 0, 1, 0.5); Y = Clamp(Y, 0, 1, 0.5); Scale = Clamp(Scale, 0.1, 4, 1);
        }
        private static double Clamp(double value, double minimum, double maximum, double fallback) =>
            double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(minimum, Math.Min(maximum, value));
    }
    [DataContract]
    public sealed class DisplayStatus
    {
        [DataMember] public long Timestamp { get; set; }
        [DataMember] public int ProcessId { get; set; }
        [DataMember] public bool Connected { get; set; }
        [DataMember] public bool Editing { get; set; }
        [DataMember] public bool Visible { get; set; }
        [DataMember] public string Screen { get; set; }
        [DataMember] public string Error { get; set; }
        [DataMember] public string[] Screens { get; set; }
        [DataMember] public string Style { get; set; }
        [DataMember] public bool Loading { get; set; }
        [DataMember] public long LastTestRequest { get; set; }
        [DataMember] public string TestError { get; set; }
    }
    [DataContract]
    public sealed class GameBarDisplayStatus
    {
        [DataMember] public long Timestamp { get; set; }
        [DataMember] public bool Blocked { get; set; }
    }
    [DataContract]
    public sealed class DisplayStopResult
    {
        [DataMember] public long ModeRequest { get; set; }
        [DataMember] public bool Stopped { get; set; }
        [DataMember] public string Error { get; set; }
    }
}
