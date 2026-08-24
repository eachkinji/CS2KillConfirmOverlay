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

        private double GetAuxiliaryLayerBaseVerticalOffset()
        {
            return GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current)
                ? 0.0
                : GetUpperThirdOffset();
        }

        private void OnAnimationLayerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAnimationDragOutlineSize();
            if (_animationPlacement == AnimationPlacementMode.Bottom
                || _animationPlacement == AnimationPlacementMode.Top)
            {
                ApplyAnimationOffset();
            }
            if (GameStyleService.Current == GameStyleMode.Overwatch
                || GameStyleService.Current == GameStyleMode.Apex
                || GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                ApplyOverwatchCardTransform();
            }
            if (GameStyleService.Current == GameStyleMode.ModernWarfare2019
                || GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current))
            {
                ApplyModernWarfare2019UpperTransform();
            }
        }

        private double GetResolvedAnimationOffset()
        {
            switch (_animationPlacement)
            {
                case AnimationPlacementMode.Bottom:
                    return GetBottomOffset();
                case AnimationPlacementMode.Top:
                    return GetTopOffset();
                case AnimationPlacementMode.Center:
                    return 0;
                default:
                    return _animationOffset;
            }
        }

        private double GetTopOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return -Math.Max(AnimationOffsetStep, layerHeight * EdgeFifthAnimationOffsetRatio);
        }

        private double GetBottomOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * EdgeFifthAnimationOffsetRatio);
        }

        private double GetUpperThirdOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return -Math.Max(AnimationOffsetStep, layerHeight / 6.0);
        }

        private double GetMaxAnimationHorizontalOffset()
        {
            double layerWidth = AnimationLayer.ActualWidth;
            if (layerWidth <= 0)
            {
                layerWidth = DefaultWidgetSize.Width;
            }

            return Math.Max(AnimationOffsetStep, layerWidth * MaxAnimationOffsetRatio);
        }

        private double GetMaxAnimationOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * MaxAnimationOffsetRatio);
        }
    }
}
