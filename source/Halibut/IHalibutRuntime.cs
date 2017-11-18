using System;
using System.Collections.Generic;
using System.Net;
using Halibut.Diagnostics;

namespace Halibut
{
    public interface IHalibutRuntime : IDisposable
    {
        ILogFactory Logs { get; }
        int Listen();
        int Listen(int port);
        int Listen(IPEndPoint endpoint);
#if HAS_WEB_SOCKET_LISTENER
        void ListenWebSocket(string endpoint);
#endif
        void Poll(Uri subscription, ServiceEndPoint endPoint);
        ServiceEndPoint Discover(Uri uri);
        ServiceEndPoint Discover(ServiceEndPoint endpoint);
        TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint);
        TService CreateClient<TService>(ServiceEndPoint endpoint);
        void Trust(string clientThumbprint);
        void RemoveTrust(string clientThumbprint);
        void TrustOnly(IReadOnlyList<string> thumbprints);
        bool IsTrusted(string remoteThumbprint);

        void Route(ServiceEndPoint to, ServiceEndPoint via);
        void SetFriendlyHtmlPageContent(string html);
    }
}