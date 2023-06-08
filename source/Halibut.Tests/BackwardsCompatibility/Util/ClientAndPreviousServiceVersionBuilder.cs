using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Tests.Util;
using Halibut.Tests.Util.TcpUtils;

namespace Halibut.Tests.BackwardsCompatibility.Util
{
    public class ClientAndPreviousServiceVersionBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        string version = null;
        HalibutRuntime? existingOctopus;
        int? existingListeningPort;

        ClientAndPreviousServiceVersionBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static ClientAndPreviousServiceVersionBuilder WithPollingService()
        {
            return new ClientAndPreviousServiceVersionBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static ClientAndPreviousServiceVersionBuilder WithListeningService()
        {
            return new ClientAndPreviousServiceVersionBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static ClientAndPreviousServiceVersionBuilder ForServiceConnectionType(ServiceConnectionType serviceConnectionType)
        {
            return new ClientAndPreviousServiceVersionBuilder(serviceConnectionType, CertAndThumbprint.TentacleListening);
        }

        public static ClientAndPreviousServiceVersionBuilder WithService(ServiceConnectionType connectionType)
        {
            switch (connectionType)
            {
                case ServiceConnectionType.Polling:
                    return WithPollingService();
                case ServiceConnectionType.Listening:
                    return WithListeningService();
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }

        public ClientAndPreviousServiceVersionBuilder WithServiceVersion(string version)
        {
            this.version = version;
            return this;
        }

        public ClientAndPreviousServiceVersionBuilder WithExistingOctopus(HalibutRuntime octopus, int? existingListeningPort)
        {
            this.existingOctopus = octopus;
            this.existingListeningPort = existingListeningPort;
            return this;
        }

        public async Task<IClientAndService> Build()
        {
            if (version == null)
            {
                throw new Exception("The version of the service must be set.");
            }

            var useExistingOctopus = this.existingOctopus != null;

            var octopus = this.existingOctopus ?? new HalibutRuntime(clientCertAndThumbprint.Certificate2);

            if (!useExistingOctopus)
            {
                octopus.Trust(serviceCertAndThumbprint.Thumbprint);
            }

            Uri serviceUri;
            HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary;
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var listenPort = useExistingOctopus ? this.existingListeningPort!.Value : octopus.Listen();
                runningOldHalibutBinary = await new HalibutTestBinaryRunner(serviceConnectionType, listenPort, clientCertAndThumbprint, serviceCertAndThumbprint, version).Run();
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
            }
            else
            {
                runningOldHalibutBinary = await new HalibutTestBinaryRunner(serviceConnectionType, null, clientCertAndThumbprint, serviceCertAndThumbprint, version).Run();
                serviceUri = new Uri("https://localhost:" + runningOldHalibutBinary.serviceListenPort);
            }

            return new ClientAndService(octopus, runningOldHalibutBinary, serviceUri, serviceCertAndThumbprint);
        }

        public class ClientAndService : IClientAndService
        {
            readonly HalibutRuntime octopus;
            readonly HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client

            public ClientAndService(HalibutRuntime octopus,
                HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint)
            {
                this.octopus = octopus;
                this.runningOldHalibutBinary = runningOldHalibutBinary;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            }

            public IHalibutRuntime Octopus => octopus;
            public PortForwarder PortForwarder => throw new NotSupportedException();

            public TService CreateClient<TService>()
            {
                return CreateClient<TService>(s => { }, CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return CreateClient<TService>(modifyServiceEndpoint, CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint);
                modifyServiceEndpoint(serviceEndpoint);
                return octopus.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint);
                modifyServiceEndpoint(serviceEndpoint);
                return octopus.CreateClient<TService, TClientService>(serviceEndpoint);
            }

            public void Dispose()
            {
                octopus.Dispose();
                runningOldHalibutBinary.Dispose();
            }
        }
    }
}