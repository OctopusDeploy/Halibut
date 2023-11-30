using System;
using System.Threading.Tasks;
using NSubstitute.Exceptions;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public class LatestClient : IClient
    {
        readonly string thumbprint;
        readonly PortForwarder? portForwarder;
        readonly ProxyDetails? proxyDetails;
        readonly ServiceConnectionType serviceConnectionType;
        readonly DisposableCollection disposableCollection;

        Uri? serviceUriThatDoesNotExist;
        TCPListenerWhichKillsNewConnections? tcpListenerWhichKillsNewConnections;

        public LatestClient(
            HalibutRuntime client,
            Uri? listeningUri,
            string thumbprint,
            PortForwarder? portForwarder,
            ProxyDetails? proxyDetails,
            ServiceConnectionType serviceConnectionType,
            DisposableCollection disposableCollection)
        {
            Client = client;
            ListeningUri = listeningUri;
            this.thumbprint = thumbprint;
            this.portForwarder = portForwarder;
            this.proxyDetails = proxyDetails;
            this.serviceConnectionType = serviceConnectionType;
            this.disposableCollection = disposableCollection;
        }

        public HalibutRuntime Client { get; }
        public Uri? ListeningUri { get; }

        public TAsyncClientService CreateClient<TService, TAsyncClientService>(Uri serviceUri)
        {
            var serviceEndPoint = GetServiceEndPoint(serviceUri);
            return Client.CreateAsyncClient<TService, TAsyncClientService>(serviceEndPoint);
        }

        public TAsyncClientService CreateClient<TService, TAsyncClientService>(Uri serviceUri, Action<ServiceEndPoint> modifyServiceEndpoint)
        {
            var serviceEndPoint = GetServiceEndPoint(serviceUri);
            modifyServiceEndpoint(serviceEndPoint);
            return Client.CreateAsyncClient<TService, TAsyncClientService>(serviceEndPoint);
        }

        public TAsyncClientService CreateClientWithoutService<TService, TAsyncClientService>()
        {
            var serviceThatDoesNotExistUri = GetServiceUriThatDoesNotExist();
            return CreateClient<TService, TAsyncClientService>(serviceThatDoesNotExistUri);
        }

        public TAsyncClientService CreateClientWithoutService<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
        {
            var serviceThatDoesNotExistUri = GetServiceUriThatDoesNotExist();
            var serviceEndPoint = GetServiceEndPoint(serviceThatDoesNotExistUri);
            modifyServiceEndpoint(serviceEndPoint);
            return Client.CreateAsyncClient<TService, TAsyncClientService>(serviceEndPoint);
        }

        public ServiceEndPoint GetServiceEndPoint(Uri serviceUri)
        {
            var serviceEndPoint = new ServiceEndPoint(serviceUri, thumbprint, proxyDetails, Client.TimeoutsAndLimits);
            return serviceEndPoint;
        }
        
        Uri GetServiceUriThatDoesNotExist()
        {
            if (serviceUriThatDoesNotExist is not null)
            {
                return serviceUriThatDoesNotExist;
            }
            
            switch (serviceConnectionType)
            {
                case ServiceConnectionType.Polling:
                    serviceUriThatDoesNotExist = LatestServiceBuilder.PollingTentacleServiceUri;
                    break;
                case ServiceConnectionType.PollingOverWebSocket:
                    serviceUriThatDoesNotExist = LatestServiceBuilder.PollingOverWebSocketTentacleServiceUri;
                    break;
                case ServiceConnectionType.Listening:
                    // Use TCPListenerWhichKillsNewConnections as a way to ensure that we do not choose a port that might actually be in use. And if we do connect, it gets rejected.
                    if (tcpListenerWhichKillsNewConnections is not null) throw new InvalidOperationException("Cannot create multiple TCPListenerWhichKillsNewConnections");
                    tcpListenerWhichKillsNewConnections = new TCPListenerWhichKillsNewConnections();
                    serviceUriThatDoesNotExist = LatestServiceBuilder.ListeningTentacleServiceUri(tcpListenerWhichKillsNewConnections.Port);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return serviceUriThatDoesNotExist;
        }

        public async ValueTask DisposeAsync()
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<LatestClient>();

            logger.Information("****** ****** ****** ****** ****** ****** ******");
            logger.Information("****** CLIENT DISPOSE CALLED  ******");
            logger.Information("*     Subsequent errors should be ignored      *");
            logger.Information("****** ****** ****** ****** ****** ****** ******");

            void LogError(Exception e) => logger.Warning(e, "Ignoring error in dispose");

            await Try.DisposingAsync(Client, LogError);

            Try.CatchingError(() => portForwarder?.Dispose(), LogError);
            Try.CatchingError(disposableCollection.Dispose, LogError);
            Try.CatchingError(() => tcpListenerWhichKillsNewConnections?.Dispose(), LogError);
        }
    }
}