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

                return CreateSyncHalibutProxyAndAdaptItToAnAsyncInterface<TService, TClientService>(halibutRuntime, serviceEndpoint);
            }

            throw new Exception("It is unclear how this was reached.");
        }

        
        
        static TAsyncClientService CreateSyncHalibutProxyAndAdaptItToAnAsyncInterface<TServiceWhichMustBeSync, TAsyncClientService>(HalibutRuntime halibutRuntime, ServiceEndPoint serviceEndpoint)
        {
            var syncVersionType = typeof(TServiceWhichMustBeSync);
            
            var syncVersion = halibutRuntime.CreateClient<TServiceWhichMustBeSync>(serviceEndpoint);

            var syncToAsyncAdaptor = DispatchProxyAsync.Create<TAsyncClientService, AdaptSyncProxyToAsyncProxy>();
            (syncToAsyncAdaptor as AdaptSyncProxyToAsyncProxy).Configure(syncVersion, syncVersionType);

            return syncToAsyncAdaptor;
        }
    }
}