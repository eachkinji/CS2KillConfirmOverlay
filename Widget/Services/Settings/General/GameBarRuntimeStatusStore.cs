using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class GameBarRuntimeStatus
    {
        internal bool IsAvailable { get; set; }
        internal bool IsPinned { get; set; }
        internal bool IsClickThroughEnabled { get; set; }
        internal DateTimeOffset UpdatedAtUtc { get; set; }
    }

    internal static class GameBarRuntimeStatusStore
    {
        private const string StateKey = "GameBarRuntimeStatus";
        private const string ActiveKey = "Active";
        private const string PinnedKey = "Pinned";
        private const string ClickThroughKey = "ClickThroughEnabled";
        private const string UpdatedTicksKey = "UpdatedUtcTicks";
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan FreshnessWindow = TimeSpan.FromSeconds(5);

        private static DateTimeOffset _lastPublishedAtUtc;
        private static bool? _lastPinned;
        private static bool? _lastClickThroughEnabled;

        internal static void Publish(bool isPinned, bool isClickThroughEnabled)
        {
            try
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                bool changed = _lastPinned != isPinned
                    || _lastClickThroughEnabled != isClickThroughEnabled;
                if (!changed && now - _lastPublishedAtUtc < HeartbeatInterval)
                {
                    return;
                }

                var state = new ApplicationDataCompositeValue
                {
                    [ActiveKey] = true,
                    [PinnedKey] = isPinned,
                    [ClickThroughKey] = isClickThroughEnabled,
                    [UpdatedTicksKey] = now.UtcDateTime.Ticks
                };
                ApplicationData.Current.LocalSettings.Values[StateKey] = state;
                _lastPinned = isPinned;
                _lastClickThroughEnabled = isClickThroughEnabled;
                _lastPublishedAtUtc = now;
            }
            catch (Exception)
            {
            }
        }

        internal static void MarkInactive()
        {
            try
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                var state = new ApplicationDataCompositeValue
                {
                    [ActiveKey] = false,
                    [PinnedKey] = _lastPinned ?? false,
                    [ClickThroughKey] = _lastClickThroughEnabled ?? false,
                    [UpdatedTicksKey] = now.UtcDateTime.Ticks
                };
                ApplicationData.Current.LocalSettings.Values[StateKey] = state;
                _lastPublishedAtUtc = now;
            }
            catch (Exception)
            {
            }
        }

        internal static GameBarRuntimeStatus Read()
        {
            try
            {
                var result = new GameBarRuntimeStatus();
                if (!(ApplicationData.Current.LocalSettings.Values[StateKey]
                    is ApplicationDataCompositeValue state))
                {
                    return result;
                }

                bool active = ReadBool(state, ActiveKey);
                long ticks = ReadLong(state, UpdatedTicksKey);
                if (ticks <= 0)
                {
                    return result;
                }

                try
                {
                    result.UpdatedAtUtc = new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
                }
                catch (ArgumentOutOfRangeException)
                {
                    return new GameBarRuntimeStatus();
                }

                TimeSpan age = DateTimeOffset.UtcNow - result.UpdatedAtUtc;
                result.IsAvailable = active && age >= TimeSpan.Zero && age <= FreshnessWindow;
                result.IsPinned = ReadBool(state, PinnedKey);
                result.IsClickThroughEnabled = ReadBool(state, ClickThroughKey);
                return result;
            }
            catch (Exception)
            {
                return new GameBarRuntimeStatus();
            }
        }

        private static bool ReadBool(ApplicationDataCompositeValue state, string key)
        {
            return state.TryGetValue(key, out object value)
                && value is bool boolValue
                && boolValue;
        }

        private static long ReadLong(ApplicationDataCompositeValue state, string key)
        {
            return state.TryGetValue(key, out object value) && value is long longValue
                ? longValue
                : 0;
        }
    }
}
