using System;
using System.Collections.Generic;

namespace KillConfirmCompatibility.Danmaku.Engine
{
    internal static class DanmakuLaneLayout
    {
        private sealed class LaneBand
        {
            public double Start { get; set; }
            public double End { get; set; }
            public double Length { get { return Math.Max(0, End - Start); } }
        }

        public static IReadOnlyList<float> Build(
            DanmakuDisplayArea area,
            double height,
            int laneCount,
            double fontSize)
        {
            laneCount = Math.Max(
                1,
                Math.Min(DanmakuReactionPolicies.EventMaximumVisibleCount, laneCount));
            double safeHeight = Math.Max(120, height);
            double bottom = Math.Max(12, safeHeight - fontSize - 10);
            var bands = new List<LaneBand>();

            switch (area)
            {
                case DanmakuDisplayArea.Top:
                    bands.Add(CreateBand(10, safeHeight * 0.48, fontSize));
                    break;
                case DanmakuDisplayArea.Bottom:
                    bands.Add(CreateBand(safeHeight * 0.52, bottom, fontSize));
                    break;
                case DanmakuDisplayArea.Center:
                    bands.Add(CreateBand(safeHeight * 0.25, safeHeight * 0.75, fontSize));
                    break;
                case DanmakuDisplayArea.AvoidCenter:
                    bands.Add(CreateBand(10, safeHeight * 0.32, fontSize));
                    bands.Add(CreateBand(safeHeight * 0.68, bottom, fontSize));
                    break;
                case DanmakuDisplayArea.All:
                default:
                    bands.Add(CreateBand(10, bottom, fontSize));
                    break;
            }

            // Reserve real line height before choosing tracks. Dense events in
            // a short area must queue horizontally instead of overlapping rows.
            double lineHeight = Math.Max(20, fontSize * 1.35 + 4);
            var candidates = new List<float>();
            foreach (var band in bands)
            {
                int count = Math.Max(1, (int)Math.Floor(band.Length / lineHeight) + 1);
                double start = band.Start + (band.Length - (count - 1) * lineHeight) / 2;
                for (int i = 0; i < count; i++) candidates.Add((float)(start + i * lineHeight));
            }
            laneCount = Math.Min(laneCount, candidates.Count);
            var lanes = new List<float>(laneCount);
            for (int i = 0; i < laneCount; i++)
            {
                int index = Math.Min(candidates.Count - 1, (int)((i + 0.5) * candidates.Count / laneCount));
                lanes.Add(candidates[index]);
            }
            return lanes;
        }

        private static LaneBand CreateBand(double start, double end, double fontSize)
        {
            double halfLine = Math.Max(8, fontSize * 0.55);
            double safeStart = Math.Max(0, start + halfLine);
            double safeEnd = Math.Max(safeStart, end - halfLine);
            return new LaneBand { Start = safeStart, End = safeEnd };
        }
    }
}
