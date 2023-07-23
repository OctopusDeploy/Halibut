using System;
using Halibut.Tests.Support.BackwardsCompatibility;

namespace Halibut.Tests.Support.TestCases
{
    /// <summary>
    /// Defines what version of the Client and the Service that will be used in a test.
    /// </summary>
    public class ClientAndServiceTestVersion
    {
        
        // null means latest.
        public HalibutVersion? ClientVersion;
        // null means latest.
        public HalibutVersion? ServiceVersion;

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
