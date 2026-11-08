// Shared by Widget (UWP) and CompatibilityHost; see Shared/Events/KillEventContract.cs.
using System;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
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
}
