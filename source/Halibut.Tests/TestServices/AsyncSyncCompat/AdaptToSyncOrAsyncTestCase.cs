using System;
using System.Reflection;
using System.Threading;
using Halibut.Tests.Support;

namespace Halibut.Tests.TestServices.AsyncSyncCompat
{
    public class AdaptToSyncOrAsyncTestCase
    {
        /// <summary>
        /// Doesn't actually adapt anything, just helps make sure we are testing what we expect to test.
        /// </summary>
        /// <param name="forceClientProxyType"></param>
        /// <param name="halibutRuntime"></param>
        /// <param name="serviceEndpoint"></param>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public TService Adapt<TService>(ForceClientProxyType? forceClientProxyType, HalibutRuntime halibutRuntime, ServiceEndPoint serviceEndpoint, CancellationToken cancellationToken)
        {
            if (forceClientProxyType == null)
            {
                return halibutRuntime.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }
            throw new Exception("Can't force sync or async with single generic type CreateClient, use CreateClient<TService, TClientService>");
        }

        public TClientService Adapt<TService, TClientService>(ForceClientProxyType? forceClientProxyType, HalibutRuntime halibutRuntime, ServiceEndPoint serviceEndpoint)
        {
            return Adapt<TService, TService, TClientService>(forceClientProxyType, halibutRuntime, serviceEndpoint);
        }

        public TClientService Adapt<TService, TSyncClientService, TClientService>(ForceClientProxyType? forceClientProxyType, HalibutRuntime halibutRuntime, ServiceEndPoint serviceEndpoint)
        {
            if (forceClientProxyType == null)
            {
                return halibutRuntime.CreateClient<TService, TClientService>(serviceEndpoint);
            }

            if (forceClientProxyType == ForceClientProxyType.AsyncClient)
            {
                new ServiceInterfaceInspector().EnsureAllMethodsAreAsync<TClientService>();
                return halibutRuntime.CreateAsyncClient<TService, TClientService>(serviceEndpoint);
            }

            if (forceClientProxyType == ForceClientProxyType.SyncClient)
            {
                new ServiceInterfaceInspector().EnsureAllMethodsAreAsync<TClientService>();

                return CreateSyncHalibutProxyAndAdaptItToAnAsyncInterface<TService, TSyncClientService, TClientService>(halibutRuntime, serviceEndpoint);
            }

            throw new Exception("It is unclear how this was reached.");
        }
        
        /// <summary>
        /// This will ask the HalibutRuntime to talk to a service of type TService e.g. IEchoService. It asks the
        /// HalibutRuntime to create a proxy of type TServiceWhichMustBeSync e.g. ISyncClientEchoServiceWithOptions
        /// This finally wraps that resulting proxy in an async one e.g. IAsyncClientEchoServiceWithOptions
        /// 
        /// </summary>
        /// <param name="halibutRuntime"></param>
        /// <param name="serviceEndpoint"></param>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TServiceWhichMustBeSync"></typeparam>
        /// <typeparam name="TAsyncClientService"></typeparam>
        /// <returns></returns>
        static TAsyncClientService CreateSyncHalibutProxyAndAdaptItToAnAsyncInterface<TService, TServiceWhichMustBeSync, TAsyncClientService>(HalibutRuntime halibutRuntime, ServiceEndPoint serviceEndpoint)
        {
            
            var syncVersion = halibutRuntime.CreateClient<TService, TServiceWhichMustBeSync>(serviceEndpoint);
            
            var syncClientTypeToAdaptTo = typeof(TServiceWhichMustBeSync);

            var syncToAsyncAdaptor = DispatchProxyAsync.Create<TAsyncClientService, AdaptSyncProxyToAsyncProxy>();
            (syncToAsyncAdaptor as AdaptSyncProxyToAsyncProxy).Configure(syncVersion, syncClientTypeToAdaptTo);

            return syncToAsyncAdaptor;
        }
    }
}