using System;

namespace KillConfirmCompatibility.Desktop.Rendering
{
    // Gate presentation against the compositor timestamp, retaining fractional
    // frame time instead of adding a new timer delay after every rendered frame.
    internal sealed class FrameCadence
    {
        private double _nextSeconds;
        private TimeSpan? _lastTimestamp;
        private int _fps;

        internal bool ShouldPresent(TimeSpan timestamp, int framesPerSecond)
        {
            if (_lastTimestamp == timestamp) return false;
            int fps = framesPerSecond <= 30 ? 30 : 60;
            double now = timestamp.TotalSeconds, interval = 1.0 / fps;
            if (!_lastTimestamp.HasValue || timestamp < _lastTimestamp.Value || fps != _fps)
                _nextSeconds = now;
            _lastTimestamp = timestamp;
            _fps = fps;
            if (now + 0.0005 < _nextSeconds) return false;
            _nextSeconds += interval;
            // After a stall, render the current animation time once; do not
            // replay missed frames or spend subsequent refreshes catching up.
            if (_nextSeconds <= now) _nextSeconds += (Math.Floor((now - _nextSeconds) / interval) + 1) * interval;
            return true;
        }
    }
}
