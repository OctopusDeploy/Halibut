using System;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public static class PreviousVersions
    {
        // The earliest release with a stable WebSocket implementation
        static readonly Version v6_0_658_WebSocket_Stability_Fixes = new("6.0.658");

        // The earliest release with a stable streams where communication won't hang sometimes on net6.0
        static readonly Version v5_0_236_Stable_Streams = new("5.0.236");

        // The last release with meaningful changes prior to Script Service V2
        public static readonly HalibutVersions v5_0_236_Used_In_Tentacle_6_3_417 = new(new Version("5.0.236"), pollingOverWebSocketServiceVersion: v6_0_658_WebSocket_Stability_Fixes);
        
#if NETFRAMEWORK
        public static readonly HalibutVersions v4_4_8 = new(new Version("4.4.8"), pollingOverWebSocketServiceVersion: v6_0_658_WebSocket_Stability_Fixes);
#else
        // A net6.0 client causes an older halibut service without buffer over-read changes to be prone to hanging or erroring when reading the stream.
        // This instability means we cannot really have automated tests for net6.0 and older these older Halibut Services
        public static readonly HalibutVersions v4_4_8 = new(new Version("4.4.8"), pollingOverWebSocketServiceVersion: v6_0_658_WebSocket_Stability_Fixes, pollingServiceVersion: v5_0_236_Stable_Streams, listeningServiceVersion: v5_0_236_Stable_Streams);
#endif
    }

    public class HalibutVersion
    {
        readonly Version pollingVersion;
        readonly Version listeningVersion;
        readonly Version pollingOverWebSocketVersion;

        internal HalibutVersion(
            Version version,
            Version? pollingVersion = null,
            Version? listeningVersion = null,
            Version? pollingOverWebSocketVersion = null)
        {
            this.pollingVersion = pollingVersion ?? version;
            this.listeningVersion = listeningVersion ?? version;
            this.pollingOverWebSocketVersion = pollingOverWebSocketVersion ?? version;
        }

        public Version ForServiceConnectionType(ServiceConnectionType serviceConnectionType)
        {
            return serviceConnectionType switch
            {
                ServiceConnectionType.Polling => pollingVersion,
                ServiceConnectionType.Listening => listeningVersion,
                ServiceConnectionType.PollingOverWebSocket => pollingOverWebSocketVersion,
                _ => throw new ArgumentOutOfRangeException(nameof(serviceConnectionType))
            };
        }

        public override string ToString()
        {
            return $"polling;{pollingVersion};listening;{listeningVersion};pollingOverWebSockets;{pollingOverWebSocketVersion};";
        }
    }

    public class HalibutVersions
    {
        public HalibutVersion ClientVersion { get; }
        public HalibutVersion ServiceVersion { get; }

        internal HalibutVersions(
            Version version,
            Version? pollingClientVersion = null,
            Version? pollingServiceVersion = null,
            Version? listeningClientVersion = null,
            Version? listeningServiceVersion = null,
            Version? pollingOverWebSocketClientVersion = null,
            Version? pollingOverWebSocketServiceVersion = null)
        {
            this.ClientVersion = new HalibutVersion(version, pollingClientVersion, listeningClientVersion, pollingOverWebSocketClientVersion);
            this.ServiceVersion = new HalibutVersion(version, pollingServiceVersion, listeningServiceVersion, pollingOverWebSocketServiceVersion);
        }
    }
}
