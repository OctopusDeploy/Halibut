using System;
using Halibut.Tests.Support.BackwardsCompatibility;
using Xunit.Abstractions;

namespace Halibut.Tests.Support.TestCases
{
    /// <summary>
    /// Defines what version of the Client and the Service that will be used in a test.
    /// </summary>
    public class ClientAndServiceTestVersion : IEquatable<ClientAndServiceTestVersion>, IXunitSerializable
    {
        
        // null means latest.
        public HalibutVersion? ClientVersion { get; private set; }
        // null means latest.
        public HalibutVersion? ServiceVersion { get; private set; }

        public ClientAndServiceTestVersion()
        {
        }

        ClientAndServiceTestVersion(HalibutVersion? clientVersion, HalibutVersion? serviceVersion)
        {
            ClientVersion = clientVersion;
            ServiceVersion = serviceVersion;
        }

        public static ClientAndServiceTestVersion Latest()
        {
            return new ClientAndServiceTestVersion(null, null);
        }

        public static ClientAndServiceTestVersion ClientOfVersion(HalibutVersion clientVersions)
        {
            return new ClientAndServiceTestVersion(clientVersions, null);
        }

        public static ClientAndServiceTestVersion ServiceOfVersion(HalibutVersion serviceVersions)
        {
            return new ClientAndServiceTestVersion(null, serviceVersions);
        }

        public bool IsLatest()
        {
            return ClientVersion == null && ServiceVersion == null;
        }

        public bool IsPreviousClient()
        {
            return ClientVersion != null;
        }

        public bool IsPreviousService()
        {
            return ServiceVersion != null;
        }

        public string ToString(ServiceConnectionType serviceConnectionType)
        {
            if (IsLatest())
            {
                return "v:Latest";
            }

            if (IsPreviousClient())
            {
                return $"vClient:{ClientVersion!.ForServiceConnectionType(serviceConnectionType)};vService:Latest";
            }

            if (IsPreviousService())
            {
                return $"vClient:Latest;vService:{ServiceVersion!.ForServiceConnectionType(serviceConnectionType)}";
            }

            throw new Exception("Invalid client and service version.");
        }
        
        public string ToShortString(ServiceConnectionType serviceConnectionType)
        {
            if (IsLatest())
            {
                return "v:Latest";
            }

            if (IsPreviousClient())
            {
                return $"vClient:{ClientVersion!.ForServiceConnectionType(serviceConnectionType)}";
            }

            if (IsPreviousService())
            {
                return $"vService:{ServiceVersion!.ForServiceConnectionType(serviceConnectionType)}";
            }

            throw new Exception("Invalid client and service version.");
        }

        public override string ToString()
        {
            if (IsLatest())
            {
                return "v:Latest";
            }

            if (IsPreviousClient())
            {
                return $"vClient:{ClientVersion};vService:Latest";
            }

            if (IsPreviousService())
            {
                return $"vClient:Latest;vService:{ServiceVersion}";
            }

            throw new Exception("Invalid client and service version.");
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ClientAndServiceTestVersion);
        }

        public bool Equals(ClientAndServiceTestVersion? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(ClientVersion, other.ClientVersion) && Equals(ServiceVersion, other.ServiceVersion);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (ClientVersion != null ? ClientVersion.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ServiceVersion != null ? ServiceVersion.GetHashCode() : 0);
                return hashCode;
            }
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            ClientVersion = info.GetValue<HalibutVersion>(nameof(ClientVersion));
            ServiceVersion = info.GetValue<HalibutVersion>(nameof(ServiceVersion));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(ClientVersion), ClientVersion);
            info.AddValue(nameof(ServiceVersion), ServiceVersion);
        }
    }

    public class ClientAndServiceBuilderFactory
    {
        public static Func<ServiceConnectionType, IClientAndServiceBuilder> ForVersion(ClientAndServiceTestVersion version)
        {
            if (version.IsLatest())
            {
                return LatestClientAndLatestServiceBuilder.ForServiceConnectionType;
            }

            if (version.IsPreviousClient())
            {
                return sct => PreviousClientVersionAndLatestServiceBuilder
                    .ForServiceConnectionType(sct)
                    .WithClientVersion(version.ClientVersion!.ForServiceConnectionType(sct));
            }

            if (version.IsPreviousService())
            {
                return sct => LatestClientAndPreviousServiceVersionBuilder
                    .ForServiceConnectionType(sct)
                    .WithServiceVersion(version.ServiceVersion!.ForServiceConnectionType(sct));
            }

            throw new Exception($"We don't know what kind of thing to build here: version={version}");
        }
    }
}
