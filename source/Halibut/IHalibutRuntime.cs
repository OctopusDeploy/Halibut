using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading;
using Halibut.Diagnostics;

namespace Halibut
{
    public enum UnauthorizedClientConnectResponse
    {
        BlockConnection,
        TrustAndAllowConnection
    }

    public interface IHalibutRuntime : IDisposable
    {
        ILogFactory Logs { get; }
        int Listen();
        int Listen(int port);
        int Listen(IPEndPoint endpoint);
        void ListenWebSocket(string endpoint);
        void Poll(Uri subscription, ServiceEndPoint endPoint);
        void Poll(Uri subscription, ServiceEndPoint endPoint, CancellationToken cancellationToken);
        ServiceEndPoint Discover(Uri uri);
        ServiceEndPoint Discover(Uri uri, CancellationToken cancellationToken);
        ServiceEndPoint Discover(ServiceEndPoint endpoint);
        ServiceEndPoint Discover(ServiceEndPoint endpoint, CancellationToken cancellationToken);
        TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint);
        TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint, CancellationToken cancellationToken);
        TService CreateClient<TService>(ServiceEndPoint endpoint);
        TService CreateClient<TService>(ServiceEndPoint endpoint, ServiceEndPoint via);
        TService CreateClient<TService>(ServiceEndPoint endpoint, CancellationToken cancellationToken);
        TService CreateClient<TService>(ServiceEndPoint endpoint, ServiceEndPoint via, CancellationToken cancellationToken);
        void Trust(string clientThumbprint);
        void RemoveTrust(string clientThumbprint);
        void TrustOnly(IReadOnlyList<string> thumbprints);
        bool IsTrusted(string remoteThumbprint);

        void AddOrUpdateRoute(ServiceEndPoint to, ServiceEndPoint via);
        void RemoveRoute(ServiceEndPoint to);
        void SetFriendlyHtmlPageContent(string html);
        void Disconnect(ServiceEndPoint endpoint);
        Func<string, string, UnauthorizedClientConnectResponse> OnUnauthorizedClientConnect { get; set; }
    }
}