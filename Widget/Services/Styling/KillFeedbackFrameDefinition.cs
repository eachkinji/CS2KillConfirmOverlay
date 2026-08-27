using System;

namespace KillConfirmGameBar.Services
{
    internal enum KillFeedbackLayer
    {
        Crosshair,
        Lower,
        Upper
    }

    // These slots describe old saved settings only, never the role of a UI frame.
    internal enum LegacyFeedbackPlacementSlot
    {
        Primary,
        LowerCard,
        Auxiliary
    }

    internal static class KillFeedbackFrameDefinition
    {
        public static KillFeedbackLayer GetLegacyPrimaryLayer(GameStyleMode style)
        {
            return style == GameStyleMode.Overwatch
                || style == GameStyleMode.Apex
                || style == GameStyleMode.ModernWarfare2019
                    ? KillFeedbackLayer.Crosshair
                    : KillFeedbackLayer.Lower;
        }

        public static KillFeedbackLayer GetLegacyAuxiliaryLayer(GameStyleMode style)
        {
            return style == GameStyleMode.ModernWarfare2019
                ? KillFeedbackLayer.Upper
                : KillFeedbackLayer.Crosshair;
        }

        public static bool IsSupported(GameStyleMode style, KillFeedbackLayer layer)
        {
            return layer == KillFeedbackLayer.Crosshair
                || layer == KillFeedbackLayer.Lower
                || (layer == KillFeedbackLayer.Upper && style == GameStyleMode.ModernWarfare2019);
        }

        public static LegacyFeedbackPlacementSlot GetLegacyPlacementSlot(
            GameStyleMode style, KillFeedbackLayer layer)
        {
            if (!IsSupported(style, layer))
            {
                throw new ArgumentOutOfRangeException(nameof(layer));
            }
            if (layer == GetLegacyPrimaryLayer(style))
            {
                return LegacyFeedbackPlacementSlot.Primary;
            }
            return layer == KillFeedbackLayer.Lower
                ? LegacyFeedbackPlacementSlot.LowerCard
                : LegacyFeedbackPlacementSlot.Auxiliary;
        }

        public static string GetTitleKey(KillFeedbackLayer layer)
        {
            return "FeedbackFrame" + layer;
        }

        public static uint GetColorArgb(KillFeedbackLayer layer, bool selected)
        {
            switch (layer)
            {
                case KillFeedbackLayer.Crosshair:
                    return selected ? 0xFFFCD34Du : 0xFFF59E0Bu;
                case KillFeedbackLayer.Upper:
                    return selected ? 0xFF67E8F9u : 0xFF06B6D4u;
                case KillFeedbackLayer.Lower:
                    return selected ? 0xFFF87171u : 0xFFEF4444u;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }
    }
}
