using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using KillConfirmGameBar.Services;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation : UserControl
    {
        private sealed class SpriteMetadata
        {
            public int FrameWidth { get; set; }
            public int FrameHeight { get; set; }
            public int Frames { get; set; }
            public int Fps { get; set; }
        }

        private sealed class AnimationAsset
        {
            public AnimationAsset(SpriteMetadata metadata, Code2KillAsset codeAsset)
            {
                Metadata = metadata;
                CodeAsset = codeAsset;
            }

            public AnimationAsset(SpriteMetadata metadata, ValorantKillAsset valorantAsset)
            {
                Metadata = metadata;
                ValorantAsset = valorantAsset;
            }

            public AnimationAsset(SpriteMetadata metadata, BattlefieldKillAsset battlefieldAsset)
            {
                Metadata = metadata;
                BattlefieldAsset = battlefieldAsset;
            }

            public AnimationAsset(SpriteMetadata metadata, CsolKillAsset csolAsset)
            {
                Metadata = metadata;
                CsolAsset = csolAsset;
            }

            public SpriteMetadata Metadata { get; }
            public Code2KillAsset CodeAsset { get; }
            public ValorantKillAsset ValorantAsset { get; }
            public BattlefieldKillAsset BattlefieldAsset { get; }
            public CsolKillAsset CsolAsset { get; }
        }

        private sealed class Code2KillAsset
        {
            public Code2KillAsset(CanvasBitmap main, CanvasBitmap fx, CanvasBitmap overlay, CanvasBitmap weaponBadge)
            {
                Main = main;
                Fx = fx;
                Overlay = overlay;
                WeaponBadge = weaponBadge;
            }

            public CanvasBitmap Main { get; }
            public CanvasBitmap Fx { get; }
            public CanvasBitmap Overlay { get; }
            public CanvasBitmap WeaponBadge { get; }
        }

        private sealed class ValorantKillAsset
        {
            public string PackKey { get; set; }
            public int KillCount { get; set; }
            public bool IsHeadshot { get; set; }
            public Color Accent { get; set; } = Color.FromArgb(255, 255, 70, 85);
            public float Brightness { get; set; } = 1.0f;
            public float Contrast { get; set; } = 1.0f;
            public int SpinDirection { get; set; } = 1;
            public ValorantTextureSet Textures { get; set; }
            public CanvasBitmap Frame => Textures?.Frame;
            public CanvasBitmap Emblem => Textures?.Emblem;
            public CanvasBitmap Bar => Textures?.Bar;
            public CanvasBitmap Blade => Textures?.Blade;
            public CanvasBitmap Headshot => Textures?.Headshot;
            public CanvasBitmap BaseParticle => Textures?.BaseParticle;
            public CanvasBitmap HeroFlame => Textures?.HeroFlame;
            public CanvasBitmap LargeSparks => Textures?.LargeSparks;
            public CanvasBitmap XSparks => Textures?.XSparks;
            public ValorantDemoProfile DemoProfile { get; set; }
        }

        private sealed class ValorantTextureSet : IDisposable
        {
            private bool _disposed;

            public string PackKey { get; set; }
            public CanvasBitmap Frame { get; set; }
            public CanvasBitmap Emblem { get; set; }
            public CanvasBitmap Bar { get; set; }
            public CanvasBitmap Blade { get; set; }
            public CanvasBitmap Headshot { get; set; }
            public CanvasBitmap BaseParticle { get; set; }
            public CanvasBitmap HeroFlame { get; set; }
            public CanvasBitmap LargeSparks { get; set; }
            public CanvasBitmap XSparks { get; set; }
            public CanvasBitmap Ring { get; set; }
            public CanvasBitmap RingDissolve { get; set; }
            public CanvasBitmap FrameDissolve { get; set; }
            public CanvasBitmap BadgeDissolve { get; set; }
            public CanvasBitmap Shadow { get; set; }
            public CanvasBitmap BaseParticleT2 { get; set; }
            public CanvasBitmap BaseParticleT3 { get; set; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Frame?.Dispose();
                Emblem?.Dispose();
                Bar?.Dispose();
                Blade?.Dispose();
                Headshot?.Dispose();
                BaseParticle?.Dispose();
                HeroFlame?.Dispose();
                LargeSparks?.Dispose();
                XSparks?.Dispose();
                Ring?.Dispose();
                RingDissolve?.Dispose();
                FrameDissolve?.Dispose();
                BadgeDissolve?.Dispose();
                Shadow?.Dispose();
                BaseParticleT2?.Dispose();
                BaseParticleT3?.Dispose();
                Frame = null;
                Emblem = null;
                Bar = null;
                Blade = null;
                Headshot = null;
                BaseParticle = null;
                HeroFlame = null;
                LargeSparks = null;
                XSparks = null;
                Ring = null;
                RingDissolve = null;
                FrameDissolve = null;
                BadgeDissolve = null;
                Shadow = null;
                BaseParticleT2 = null;
                BaseParticleT3 = null;
            }
        }

        private sealed class BattlefieldKillAsset
        {
            public string StyleKey { get; set; }
            public int KillCount { get; set; }
            public bool IsHeadshot { get; set; }
            public bool IsAssist { get; set; }
            public bool IsCrit { get; set; }
            public bool IsTextOnly { get; set; }
            public string EventKind { get; set; }
            public int RoundNumber { get; set; }
            public int MoneyEpoch { get; set; }
            public string PlayerName { get; set; }
            public string WeaponLabel { get; set; }
            public string HealthText { get; set; }
            public int MoneyReward { get; set; }
            public CanvasBitmap Icon { get; set; }
        }

        private enum KillFxMode
        {
            Off = 0,
            Pack = 1,
            Original = 2
        }

        private readonly struct TransformKey
        {
            public TransformKey(double progress, double x, double y, double scale, double opacity)
            {
                Progress = progress;
                X = x;
                Y = y;
                Scale = scale;
                Opacity = opacity;
            }

            public double Progress { get; }
            public double X { get; }
            public double Y { get; }
            public double Scale { get; }
            public double Opacity { get; }

            public TransformSample ToSample()
            {
                return new TransformSample(X, Y, Scale, Opacity);
            }
        }

        private struct TransformSample
        {
            public TransformSample(double x, double y, double scale, double opacity)
            {
                X = x;
                Y = y;
                Scale = scale;
                Opacity = opacity;
            }

            public double X;
            public double Y;
            public double Scale;
            public double Opacity;
        }

    }
}
