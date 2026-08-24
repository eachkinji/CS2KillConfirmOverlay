using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    public enum KillEventConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    public enum ServiceConnectionFailureKind
    {
        ConnectFailed,
        ConnectionClosed,
        AuthenticationFailed,
        MessageReadFailed
    }

    public sealed class ServiceConnectionFailureEventArgs : EventArgs
    {
        public ServiceConnectionFailureKind Kind { get; set; }
        public int HResult { get; set; }
        public string Detail { get; set; }
    }

    public sealed class EventsDroppedEventArgs : EventArgs
    {
        public EventsDroppedEventArgs(ulong dropped)
        {
            Dropped = dropped;
        }

        public ulong Dropped { get; }
    }

    public sealed class KillEvent
    {
        public string EventChannel { get; set; }
        public int KillCount { get; set; }
        public bool IsHeadshot { get; set; }
        public bool IsKnifeKill { get; set; }
        public bool IsFirstKill { get; set; }
        public bool IsLastKill { get; set; }
        public bool IsAssist { get; set; }
        public bool PlayMainAnimation { get; set; }
        public string AnimationKey { get; set; }
        public string EventKind { get; set; }
        public string WeaponBadgeKey { get; set; }
        public string WeaponName { get; set; }
        public int MoneyReward { get; set; }
        public int RoundNumber { get; set; }
        public int MoneyEpoch { get; set; }
        public string PlayerName { get; set; }
        public string TargetName { get; set; }
        public string SteamId { get; set; }
        public ulong PublishedUnixMs { get; set; }

        public bool IsCombatEvent
        {
            get { return string.Equals(EventChannel, KillEventChannels.Combat, StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsEconomyEvent
        {
            get { return string.Equals(EventChannel, KillEventChannels.Economy, StringComparison.OrdinalIgnoreCase); }
        }
    }

    public static class KillEventChannels
    {
        public const string Combat = "combat";
        public const string Economy = "economy";

        public static string Normalize(string eventChannel, string eventKind, bool isAssist)
        {
            if (string.Equals(eventChannel, Combat, StringComparison.OrdinalIgnoreCase))
            {
                return Combat;
            }

            if (string.Equals(eventChannel, Economy, StringComparison.OrdinalIgnoreCase))
            {
                return Economy;
            }

            return IsEconomyKind(eventKind) && !isAssist ? Economy : Combat;
        }

        private static bool IsEconomyKind(string eventKind)
        {
            return string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }
    }

}
