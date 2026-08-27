using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.System;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage : Page
    {

        private sealed class TestPreset
        {
            public TestPreset(
                int killCount,
                bool isHeadshot = false,
                bool isKnifeKill = false,
                bool isGrenadeKill = false,
                bool isAssist = false,
                bool isFirstKill = false,
                bool isLastKill = false,
                bool playMainAnimation = true,
                string animationKey = null,
                string eventChannel = KillEventChannels.Combat,
                string eventKind = null,
                string weaponName = null,
                int? moneyReward = null)
            {
                KillCount = killCount;
                IsHeadshot = isHeadshot;
                IsKnifeKill = isKnifeKill;
                IsGrenadeKill = isGrenadeKill;
                IsAssist = isAssist;
                IsFirstKill = isFirstKill;
                IsLastKill = isLastKill;
                PlayMainAnimation = playMainAnimation;
                AnimationKey = animationKey;
                EventChannel = eventChannel ?? KillEventChannels.Combat;
                EventKind = eventKind ?? (isAssist ? "assist" : "kill");
                WeaponName = weaponName;
                MoneyReward = moneyReward ?? (isAssist ? 0 : (isKnifeKill ? 1500 : 300));
            }

            public int KillCount { get; }
            public bool IsHeadshot { get; }
            public bool IsKnifeKill { get; }
            public bool IsGrenadeKill { get; }
            public bool IsAssist { get; }
            public bool IsFirstKill { get; }
            public bool IsLastKill { get; }
            public bool PlayMainAnimation { get; }
            public string AnimationKey { get; }
            public string EventChannel { get; }
            public string EventKind { get; }
            public string WeaponName { get; }
            public int MoneyReward { get; }

            public KillEvent ToKillEvent()
            {
                return new KillEvent
                {
                    EventChannel = EventChannel,
                    KillCount = KillCount,
                    IsHeadshot = IsHeadshot,
                    IsKnifeKill = IsKnifeKill,
                    IsGrenadeKill = IsGrenadeKill,
                    IsAssist = IsAssist,
                    IsFirstKill = IsFirstKill,
                    IsLastKill = IsLastKill,
                    PlayMainAnimation = PlayMainAnimation,
                    AnimationKey = AnimationKey,
                    EventKind = EventKind,
                    WeaponName = WeaponName,
                    MoneyReward = MoneyReward
                };
            }
        }
    }
}
