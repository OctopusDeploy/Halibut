using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Caching;
using Halibut.Util;

namespace Halibut
{
    public interface IHalibutRuntime : IDisposable, IAsyncDisposable
    {
        ILogFactory Logs { get; }
        int Listen();
        int Listen(int port);
        int Listen(IPEndPoint endpoint);
        void ListenWebSocket(string endpoint);
        void Poll(Uri subscription, ServiceEndPoint endPoint);
        void Poll(Uri subscription, ServiceEndPoint endPoint, CancellationToken cancellationToken);

        Task<ServiceEndPoint> DiscoverAsync(Uri uri, CancellationToken cancellationToken);
        Task<ServiceEndPoint> DiscoverAsync(ServiceEndPoint endpoint, CancellationToken cancellationToken);

        [Obsolete]
        ServiceEndPoint Discover(Uri uri);
        [Obsolete]
        ServiceEndPoint Discover(Uri uri, CancellationToken cancellationToken);
        [Obsolete]
        ServiceEndPoint Discover(ServiceEndPoint endpoint);
        [Obsolete]
        ServiceEndPoint Discover(ServiceEndPoint endpoint, CancellationToken cancellationToken);
        
        /// <summary>
        /// Creates a Halibut client mapping the methods from TClientService to TService, with support for async methods
        /// </summary>
        /// <param name="endpoint"></param>
        /// <typeparam name="TService">The interface the remote service implements.</typeparam>
        /// <typeparam name="TAsyncClientService">The type that will be returned. Must have the same methods as TService except
        /// that each method may have an additional argument at the end of HalibutProxyRequestOptions. When requests are made
        /// to the service the HalibutProxyRequestOptions is dropped and the request is sent as though it was called on the
        /// equivalent method of TService.
        ///
        /// For example if TService is interface IFoo { void Bar(string); }
        /// TClientService would be: IClientFoo { void Bar(string, HalibutProxyRequestOptions); }
        /// </typeparam>
        /// <returns></returns>
        public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(ServiceEndPoint endpoint);

        [Obsolete]
        TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint);
        [Obsolete]
        TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint, CancellationToken cancellationToken);
        [Obsolete]
        TService CreateClient<TService>(ServiceEndPoint endpoint);
        [Obsolete]
        TService CreateClient<TService>(ServiceEndPoint endpoint, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a Halibut client mapping the methods from TClientService to TService.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <typeparam name="TService">The interface the remote service implements.</typeparam>
        /// <typeparam name="TClientService">The type that will be returned. Must have the same methods as TService except
        /// that each method may have an additional argument at the end of HalibutProxyRequestOptions. When requests are made
        /// to the service the HalibutProxyRequestOptions is dropped and the request is sent as though it was called on the
        /// equivalent method of TService.
        ///
        /// For example if TService is interface IFoo { void Bar(string); }
        /// TClientService would be: IClientFoo { void Bar(string, HalibutProxyRequestOptions); }
        /// </typeparam>
        /// <returns></returns>
        [Obsolete]
        public TClientService CreateClient<TService, TClientService>(ServiceEndPoint endpoint);

        void Trust(string clientThumbprint);
        [Obsolete]
        void RemoveTrust(string clientThumbprint);
        void TrustOnly(IReadOnlyList<string> thumbprints);
        bool IsTrusted(string remoteThumbprint);

        void Route(ServiceEndPoint to, ServiceEndPoint via);
        void SetFriendlyHtmlPageContent(string html);

        [Obsolete]
        void Disconnect(ServiceEndPoint endpoint);
        Task DisconnectAsync(ServiceEndPoint endpoint, CancellationToken cancellationToken);

        Func<string, string, UnauthorizedClientConnectResponse> OnUnauthorizedClientConnect { get; set; }
        OverrideErrorResponseMessageCachingAction OverrideErrorResponseMessageCaching { get; set; }
        AsyncHalibutFeature AsyncHalibutFeature { get; }
        public HalibutTimeoutsAndLimits TimeoutsAndLimits { get; }
    }
}