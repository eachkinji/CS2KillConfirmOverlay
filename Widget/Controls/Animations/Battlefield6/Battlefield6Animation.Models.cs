using System.Collections.Generic;
using Microsoft.Graphics.Canvas;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private sealed class Battlefield6HudState
        {
            public readonly Queue<Battlefield6Event> Pending = new Queue<Battlefield6Event>();
            public readonly List<Battlefield6Icon> Icons = new List<Battlefield6Icon>();
            public readonly List<Battlefield6Bonus> Bonuses = new List<Battlefield6Bonus>();
            public double LastAdmissionMs = -Battlefield6QueueMs;
            public double LastUpdateMs = -1;
            public double NextBonusFadeMs = -1;
            public string Victim = string.Empty;
            public double VictimWidth;
            public double VictimStartMs = -1;
            public bool VictimIsRescue;
            public double VictimY = Battlefield6VictimY;
            public double BonusY = Battlefield6BonusY;
            public readonly Battlefield6VerticalMotion VictimMotion = new Battlefield6VerticalMotion(Battlefield6VictimY);
            public readonly Battlefield6VerticalMotion BonusMotion = new Battlefield6VerticalMotion(Battlefield6BonusY);
            public double ScoreTarget;
            public double ScoreFrom;
            public double ScoreValue;
            public double ScoreStartMs = -1;
            public double ScoreUpdatedMs = -1;
            public double ScoreBackgroundMs = -1;
            public int DisplayScore = -1;
            public string ScoreText = string.Empty;
            public double ScoreWidth;
            public int Round = -1;
            public int Epoch = -1;

            public void Clear()
            {
                Pending.Clear(); Icons.Clear(); Bonuses.Clear();
                LastAdmissionMs = -Battlefield6QueueMs; LastUpdateMs = -1; NextBonusFadeMs = -1;
                Victim = string.Empty; VictimStartMs = -1; VictimWidth = 0;
                VictimY = Battlefield6VictimY; BonusY = Battlefield6BonusY;
                VictimMotion.Reset(Battlefield6VictimY); BonusMotion.Reset(Battlefield6BonusY);
                ScoreTarget = ScoreFrom = ScoreValue = ScoreWidth = 0;
                ScoreStartMs = ScoreUpdatedMs = ScoreBackgroundMs = -1;
                DisplayScore = -1; ScoreText = string.Empty; Round = Epoch = -1;
            }
        }

        private sealed class Battlefield6VerticalMotion
        {
            private double _from;
            private double _target;
            private double _start;
            private double _value;
            public Battlefield6VerticalMotion(double value) { Reset(value); }
            public void Reset(double value) { _from = _target = _value = value; _start = 0; }
            public double Update(double target, double now)
            {
                _value = Lerp(_from, _target, EaseOutCubic((now - _start) / 100));
                if (target != _target) { _from = _value; _target = target; _start = now; }
                return _value;
            }
        }

        private sealed class Battlefield6Event
        {
            public CanvasBitmap Bitmap;
            public bool Ready;
            public bool Headshot;
            public bool Rescue;
            public bool HasVictim;
            public string Victim;
            public string Label;
            public int Reward;
            public int Round;
            public int Epoch;
        }

        private sealed class Battlefield6Icon
        {
            public CanvasBitmap Bitmap;
            public bool Headshot;
            public double StartMs;
            public double X = Battlefield6IconX;
            public double FromX = Battlefield6IconX;
            public double TargetX = Battlefield6IconX;
            public double MoveStartMs;
            public double ForcedFadeMs = -1;
        }

        private sealed class Battlefield6Bonus
        {
            public string Label;
            public string Text;
            public int Count = 1;
            public double Width;
            public double StartMs;
            public double X;
            public double FadeStartMs = -1;
        }
    }
}
