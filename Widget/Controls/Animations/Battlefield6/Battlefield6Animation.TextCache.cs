using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private readonly Dictionary<string, CanvasTextLayout> _battlefield6TextLayouts =
            new Dictionary<string, CanvasTextLayout>(StringComparer.Ordinal);
        private CanvasTextLayout _battlefield6ScoreLayout;
        private string _battlefield6ScoreLayoutText;

        private CanvasTextLayout GetBattlefield6TextLayout(string text, CanvasTextFormat format)
        {
            if (ReferenceEquals(format, _battlefield6ScoreFormat))
            {
                if (_battlefield6ScoreLayout != null && text == _battlefield6ScoreLayoutText)
                    return _battlefield6ScoreLayout;
                _battlefield6ScoreLayout?.Dispose();
                _battlefield6ScoreLayout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, format, 1000, 100);
                _battlefield6ScoreLayoutText = text;
                return _battlefield6ScoreLayout;
            }
            if (_battlefield6TextLayouts.TryGetValue(text, out CanvasTextLayout layout)) return layout;
            // Bound distinct victims and temporary long-name trimming candidates per session.
            if (_battlefield6TextLayouts.Count >= 128)
            {
                foreach (CanvasTextLayout cached in _battlefield6TextLayouts.Values) cached.Dispose();
                _battlefield6TextLayouts.Clear();
            }
            layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, format, 1000, 100);
            _battlefield6TextLayouts[text] = layout;
            return layout;
        }

        private double MeasureBattlefield6TextWidth(string text, CanvasTextFormat format)
        {
            return string.IsNullOrEmpty(text) ? 0 : Math.Max(0, Math.Ceiling(GetBattlefield6TextLayout(text, format).DrawBounds.Width));
        }

        private void ClearBattlefield6TextLayouts()
        {
            _battlefield6ScoreLayout?.Dispose();
            _battlefield6ScoreLayout = null;
            _battlefield6ScoreLayoutText = null;
            foreach (CanvasTextLayout layout in _battlefield6TextLayouts.Values) layout.Dispose();
            _battlefield6TextLayouts.Clear();
        }
    }
}
