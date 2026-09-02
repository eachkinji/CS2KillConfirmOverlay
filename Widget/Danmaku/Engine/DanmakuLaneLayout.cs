using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
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

            double totalLength = 0;
            for (int i = 0; i < bands.Count; i++)
            {
                totalLength += bands[i].Length;
            }

            if (totalLength <= 0)
            {
                var fallback = new List<float>(laneCount);
                for (int i = 0; i < laneCount; i++)
                {
                    fallback.Add(12 + (i * (float)Math.Max(20, fontSize + 4)));
                }
                return fallback;
            }

            var lanes = new List<float>(laneCount);
            for (int lane = 0; lane < laneCount; lane++)
            {
                double cursor = totalLength * ((lane + 0.5) / laneCount);
                for (int bandIndex = 0; bandIndex < bands.Count; bandIndex++)
                {
                    LaneBand band = bands[bandIndex];
                    if (cursor <= band.Length || bandIndex == bands.Count - 1)
                    {
                        lanes.Add((float)Math.Min(band.End, band.Start + cursor));
                        break;
                    }
                    cursor -= band.Length;
                }
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
