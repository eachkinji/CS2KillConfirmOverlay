using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {

        private double GetLegacyAuxiliaryBaseVerticalOffset()
        {
            return GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current)
                ? 0.0
                : GetUpperThirdOffset();
        }

        private void OnAnimationLayerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAnimationDragOutlineSize();
            if (_legacyPrimaryPlacement == AnimationPlacementMode.Bottom
                || _legacyPrimaryPlacement == AnimationPlacementMode.Top)
            {
                ApplyAnimationOffset();
            }
            if (GameStyleService.Current == GameStyleMode.Overwatch
                || GameStyleService.Current == GameStyleMode.Apex
                || GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                ApplyLegacyLowerCardTransform();
            }
            if (GameStyleService.Current == GameStyleMode.ModernWarfare2019
                || GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current))
            {
                ApplyLegacyAuxiliaryTransform();
            }
        }

        private double GetLegacyPrimaryResolvedVerticalOffset()
        {
            switch (_legacyPrimaryPlacement)
            {
                case AnimationPlacementMode.Bottom:
                    return GetBottomOffset();
                case AnimationPlacementMode.Top:
                    return GetTopOffset();
                case AnimationPlacementMode.Center:
                    return 0;
                default:
                    return _legacyPrimaryVerticalOffset;
            }
        }

        private double GetTopOffset()
        {
            double layerHeight = LowerFeedbackLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return -Math.Max(AnimationOffsetStep, layerHeight * EdgeFifthAnimationOffsetRatio);
        }

        private double GetBottomOffset()
        {
            double layerHeight = LowerFeedbackLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * EdgeFifthAnimationOffsetRatio);
        }

        private double GetUpperThirdOffset()
        {
            double layerHeight = LowerFeedbackLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return -Math.Max(AnimationOffsetStep, layerHeight / 6.0);
        }

        private double GetMaxAnimationHorizontalOffset()
        {
            double layerWidth = LowerFeedbackLayer.ActualWidth;
            if (layerWidth <= 0)
            {
                layerWidth = DefaultWidgetSize.Width;
            }

            return Math.Max(AnimationOffsetStep, layerWidth * MaxAnimationOffsetRatio);
        }

        private double GetMaxAnimationOffset()
        {
            double layerHeight = LowerFeedbackLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * MaxAnimationOffsetRatio);
        }
    }
}
