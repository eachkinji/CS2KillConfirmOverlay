using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double BattlefieldFrameWidth = 607;
        private const double BattlefieldFrameHeight = 260;
        private const int BattlefieldTextLineHeight = 10;
        private const string BattlefieldFontFamily = "Segoe UI";
        private static readonly Dictionary<string, CanvasBitmap> BattlefieldIconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);


        private const int BattlefieldKillTypeNormal = 0;
        private const int BattlefieldKillTypeHeadshot = 1;
        private const int BattlefieldKillTypeCrit = 2;
        private const int BattlefieldKillTypeAssist = 3;
    }
}
