using System;
using Halibut.Tests.Support.BackwardsCompatibility;

namespace Halibut.Tests.Support.TestCases
{
    public class ClientServiceTestVersion
    {
        public string? ClientVersion;
        public string? ServiceVersion;

        private ClientServiceTestVersion(string? clientVersion, string? serviceVersion)
        {
            ClientVersion = clientVersion;
            ServiceVersion = serviceVersion;
        }

        public static ClientServiceTestVersion Latest()
        {
            return new ClientServiceTestVersion(null, null);
        }

        public static ClientServiceTestVersion ClientOfVersion(string clientVersion)
        {
            return new ClientServiceTestVersion(clientVersion, null);
        }

        public static ClientServiceTestVersion ServiceOfVersion(string serviceVersion)
        {
            return new ClientServiceTestVersion(null, serviceVersion);
        }

        public bool IsLatest()
        {
            return ClientVersion == null && ServiceVersion == null;
        }

        public bool IsClientOld()
        {
            return ClientVersion != null;
        }

        public bool IsServiceOld()
        {
            return ServiceVersion != null;
        }

        public override string ToString()
        {
            if (IsLatest())
            {
                return "v:Latest";
            }

            if (IsClientOld())
            {
                return $"vClient:{ClientVersion}";
            }

            if (IsServiceOld())
            {
                return $"vService:{ServiceVersion}";
            }

            return "shrug";
        }
    }

    public class ClientServiceBuilderFactory
    {
        public static Func<ServiceConnectionType, IClientAndServiceBaseBuilder> ForVersion(ClientServiceTestVersion version)
        {
            if (version.IsLatest())
            {
                return ClientServiceBuilder.ForServiceConnectionType;
            }

            if (version.IsClientOld())
            {
                return sct => PreviousClientVersionAndServiceBuilder.ForServiceConnectionType(sct).WithClientVersion(version.ClientVersion);
            }

            if (version.IsServiceOld())
            {
                return sct => ClientAndPreviousServiceVersionBuilder.ForServiceConnectionType(sct).WithServiceVersion(version.ServiceVersion);
            }

            throw new Exception($"We don't know what kind of thing to build here: version={version}");
        }
    }
}
