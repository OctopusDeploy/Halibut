#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class ClientAndPreviousServiceVersionBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        string? version = null;

        ClientAndPreviousServiceVersionBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static ClientAndPreviousServiceVersionBuilder WithPollingService()
        {
            return new ClientAndPreviousServiceVersionBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static ClientAndPreviousServiceVersionBuilder WithPollingOverWebSocketService()
        {
            return new ClientAndPreviousServiceVersionBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.TentaclePolling);
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
                case ServiceConnectionType.PollingOverWebSocket:
                    return WithPollingOverWebSocketService();
                case ServiceConnectionType.Listening:
                    return WithListeningService();
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }

        public ClientAndPreviousServiceVersionBuilder WithServiceVersion(string? version)
        {
            this.version = version;
            return this;
        }

        public async Task<IClientAndService> Build()
        {
            if (version == null)
            {
                throw new Exception("The version of the service must be set.");
            }

            var octopus = new HalibutRuntime(clientCertAndThumbprint.Certificate2);
            octopus.Trust(serviceCertAndThumbprint.Thumbprint);

            Uri serviceUri;
            HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary;
            var disposableCollection = new DisposableCollection();

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var listenPort = octopus.Listen();
                runningOldHalibutBinary = await new HalibutTestBinaryRunner(serviceConnectionType, listenPort, clientCertAndThumbprint, serviceCertAndThumbprint, version).Run();
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                var webSocketListeningPort = TcpPortHelper.FindFreeTcpPort();
                var webSocketPath = Guid.NewGuid().ToString();
                var webSocketListeningUrl = $"https://+:{webSocketListeningPort}/{webSocketPath}";
                var webSocketSslCertificateBindingAddress = $"0.0.0.0:{webSocketListeningPort}";

                octopus.ListenWebSocket(webSocketListeningUrl);

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketSslCertificateBindingAddress).Build();
                disposableCollection.Add(webSocketSslCertificate);

                var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketPath}");
                runningOldHalibutBinary = await new HalibutTestBinaryRunner(serviceConnectionType, webSocketServiceEndpointUri, clientCertAndThumbprint, serviceCertAndThumbprint, version).Run();
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                runningOldHalibutBinary = await new HalibutTestBinaryRunner(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version).Run();
                serviceUri = new Uri("https://localhost:" + runningOldHalibutBinary.serviceListenPort);
            }
            else
            {
                throw new NotSupportedException();
            }

            return new ClientAndService(octopus, runningOldHalibutBinary, serviceUri, serviceCertAndThumbprint, disposableCollection);
        }

        public class ClientAndService : IClientAndService
        {
            readonly HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;

            public ClientAndService(HalibutRuntime octopus,
                HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                DisposableCollection disposableCollection)
            {
                Octopus = octopus;
                this.runningOldHalibutBinary = runningOldHalibutBinary;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                this.disposableCollection = disposableCollection;
            }

            public HalibutRuntime Octopus { get; }

            public PortForwarder? PortForwarder => throw new NotSupportedException();

            public TService CreateClient<TService>(CancellationToken? cancellationToken = null, string? remoteThumbprint = null)
            {
                return CreateClient<TService>(s => { }, cancellationToken ?? CancellationToken.None, remoteThumbprint);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return CreateClient<TService>(modifyServiceEndpoint, CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken, string? remoteThumbprint = null)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, remoteThumbprint ?? serviceCertAndThumbprint.Thumbprint);
                modifyServiceEndpoint(serviceEndpoint);
                return Octopus.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint);
                modifyServiceEndpoint(serviceEndpoint);
                return Octopus.CreateClient<TService, TClientService>(serviceEndpoint);
            }

            public void Dispose()
            {
                Octopus.Dispose();
                runningOldHalibutBinary.Dispose();
            }
        }
    }
}