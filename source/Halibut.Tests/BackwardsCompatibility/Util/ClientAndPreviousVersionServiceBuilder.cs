using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Tests.Util;

namespace Halibut.Tests.BackwardsCompatibility.Util
{
    public class ClientAndPreviousVersionServiceBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        string version = "5.0.429";

        ClientAndPreviousVersionServiceBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static ClientAndPreviousVersionServiceBuilder WithPollingService()
        {
            return new ClientAndPreviousVersionServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static ClientAndPreviousVersionServiceBuilder WithListeningService()
        {
            return new ClientAndPreviousVersionServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static ClientAndPreviousVersionServiceBuilder WithService(ServiceConnectionType connectionType)
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

        public ClientAndPreviousVersionServiceBuilder WithServiceVersion(string version)
        {
            this.version = version;
            return this;
        }

        public async Task<ClientAndService> Build()
        {
            if (version == null) throw new Exception("The version of the service must be set.");
            var octopus = new HalibutRuntime(clientCertAndThumbprint.Certificate2);
            octopus.Trust(serviceCertAndThumbprint.Thumbprint);

            Uri serviceUri;
            HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary;
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var listenPort = octopus.Listen();
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

        public class ClientAndService : IDisposable
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