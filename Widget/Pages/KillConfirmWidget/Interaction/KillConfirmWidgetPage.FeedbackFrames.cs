using System;
using System.Collections.Generic;
using KillConfirmGameBar.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private KillFeedbackLayer? _selectedFeedbackLayer;
        private GameStyleMode _feedbackDragStyle;
        private readonly Dictionary<int, SolidColorBrush> _feedbackFrameBrushes =
            new Dictionary<int, SolidColorBrush>();

        private Border GetFeedbackFrameOutline(KillFeedbackLayer layer)
        {
            switch (layer)
            {
                case KillFeedbackLayer.Crosshair: return CrosshairDragOutline;
                case KillFeedbackLayer.Lower: return LowerDragOutline;
                case KillFeedbackLayer.Upper: return UpperDragOutline;
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        private Border GetFeedbackFrameHint(KillFeedbackLayer layer)
        {
            switch (layer)
            {
                case KillFeedbackLayer.Crosshair: return CrosshairDragHint;
                case KillFeedbackLayer.Lower: return LowerDragHint;
                case KillFeedbackLayer.Upper: return UpperDragHint;
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        private KillFeedbackLayer? GetFeedbackFrameLayer(Border outline)
        {
            if (outline == null) return null;
            if (ReferenceEquals(outline, CrosshairDragOutline)) return KillFeedbackLayer.Crosshair;
            if (ReferenceEquals(outline, LowerDragOutline)) return KillFeedbackLayer.Lower;
            if (ReferenceEquals(outline, UpperDragOutline)) return KillFeedbackLayer.Upper;
            return null;
        }

        private Controls.KillConfirmAnimation GetFeedbackAnimation(KillFeedbackLayer layer)
        {
            switch (layer)
            {
                case KillFeedbackLayer.Crosshair: return CrosshairFeedbackAnimation;
                case KillFeedbackLayer.Lower: return LowerFeedbackAnimation;
                case KillFeedbackLayer.Upper: return UpperFeedbackAnimation;
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        private CompositeTransform GetFeedbackTransform(KillFeedbackLayer layer)
        {
            switch (layer)
            {
                case KillFeedbackLayer.Crosshair: return CrosshairFeedbackTransform;
                case KillFeedbackLayer.Lower: return LowerFeedbackTransform;
                case KillFeedbackLayer.Upper: return UpperFeedbackTransform;
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        private SolidColorBrush GetFeedbackFrameBrush(KillFeedbackLayer layer, bool selected)
        {
            int key = (int)layer * 2 + (selected ? 1 : 0);
            if (!_feedbackFrameBrushes.TryGetValue(key, out SolidColorBrush brush))
            {
                uint argb = KillFeedbackFrameDefinition.GetColorArgb(layer, selected);
                brush = new SolidColorBrush(Color.FromArgb(
                    (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
                _feedbackFrameBrushes[key] = brush;
            }
            return brush;
        }

        // Only this adapter maps semantic frames to the old position/scale slots.
        // Keep the existing storage keys so upgrades do not move users' effects.
        private LegacyFeedbackPlacementSlot GetFeedbackPlacementSlot(KillFeedbackLayer layer)
        {
            return KillFeedbackFrameDefinition.GetLegacyPlacementSlot(GameStyleService.Current, layer);
        }

        private Point GetFeedbackFramePosition(KillFeedbackLayer layer)
        {
            switch (GetFeedbackPlacementSlot(layer))
            {
                case LegacyFeedbackPlacementSlot.LowerCard:
                    return new Point(_legacyLowerCardHorizontalOffset, GetBottomOffset() + _legacyLowerCardVerticalOffset);
                case LegacyFeedbackPlacementSlot.Auxiliary:
                    return new Point(_legacyAuxiliaryHorizontalOffset, GetLegacyAuxiliaryResolvedVerticalOffset());
                default:
                    return new Point(_legacyPrimaryHorizontalOffset, GetLegacyPrimaryResolvedVerticalOffset());
            }
        }

        private void SetFeedbackFramePosition(
            KillFeedbackLayer layer, double x, double y, bool save, bool preserveVerticalPlacement = false)
        {
            x = Math.Max(-GetMaxAnimationHorizontalOffset(), Math.Min(GetMaxAnimationHorizontalOffset(), x));
            y = Math.Max(-GetMaxAnimationOffset(), Math.Min(GetMaxAnimationOffset(), y));
            switch (GetFeedbackPlacementSlot(layer))
            {
                case LegacyFeedbackPlacementSlot.LowerCard:
                    _legacyLowerCardHorizontalOffset = x;
                    _legacyLowerCardVerticalOffset = y - GetBottomOffset();
                    ApplyLegacyLowerCardTransform();
                    break;
                case LegacyFeedbackPlacementSlot.Auxiliary:
                    _legacyAuxiliaryHorizontalOffset = x;
                    _legacyAuxiliaryVerticalOffset = y - GetLegacyAuxiliaryBaseVerticalOffset();
                    ApplyLegacyAuxiliaryTransform();
                    break;
                default:
                    _legacyPrimaryHorizontalOffset = x;
                    if (!preserveVerticalPlacement)
                    {
                        _legacyPrimaryPlacement = AnimationPlacementMode.Manual;
                        _legacyPrimaryVerticalOffset = y;
                    }
                    ApplyLegacyPrimaryTransform();
                    break;
            }
            if (save) SaveFeedbackFramePlacement(layer);
        }

        private void SaveFeedbackFramePlacement(KillFeedbackLayer layer)
        {
            switch (GetFeedbackPlacementSlot(layer))
            {
                case LegacyFeedbackPlacementSlot.LowerCard: SaveLegacyLowerCardPlacementSettings(); break;
                case LegacyFeedbackPlacementSlot.Auxiliary: SaveLegacyAuxiliaryPlacementSettings(); break;
                default: SaveLegacyPrimaryPlacementSettings(); break;
            }
        }

        private void ScaleFeedbackFrame(KillFeedbackLayer layer, double factor)
        {
            switch (GetFeedbackPlacementSlot(layer))
            {
                case LegacyFeedbackPlacementSlot.LowerCard: ScaleLegacyLowerCard(factor); break;
                case LegacyFeedbackPlacementSlot.Auxiliary: ScaleLegacyAuxiliary(factor); break;
                default: ScaleAnimation(factor); break;
            }
        }

        private void SetFeedbackFramePlacement(
            KillFeedbackLayer layer, AnimationPlacementMode placement, bool centerHorizontally = true)
        {
            double x = centerHorizontally ? 0 : GetFeedbackFramePosition(layer).X;
            double y = placement == AnimationPlacementMode.Top ? GetTopOffset()
                : placement == AnimationPlacementMode.Bottom ? GetBottomOffset() : 0;
            if (GetFeedbackPlacementSlot(layer) == LegacyFeedbackPlacementSlot.Primary)
            {
                _legacyPrimaryPlacement = placement;
                _legacyPrimaryHorizontalOffset = x;
                _legacyPrimaryVerticalOffset = y;
                ApplyLegacyPrimaryTransform();
                SaveLegacyPrimaryPlacementSettings();
            }
            else
            {
                SetFeedbackFramePosition(layer, x, y, save: true);
            }
        }
    }
}
