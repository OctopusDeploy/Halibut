using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Halibut.TestProxy
{
    interface IProxyConnectionService : IDisposable
    {
        IProxyConnection GetProxyConnection(ProxyEndpoint endpoint);
    }

    class ProxyConnectionService : IProxyConnectionService
    {
        readonly IProxyConnectionFactory proxyConnectionFactory;
        readonly ILogger<ProxyConnectionService> logger;
        ConcurrentDictionary<ProxyEndpoint, IProxyConnection>? proxies = new();

        public ProxyConnectionService(
            IProxyConnectionFactory proxyConnectionFactory,
            ILogger<ProxyConnectionService> logger)
        {
            this.proxyConnectionFactory = proxyConnectionFactory;
            this.logger = logger;
        }

        public IProxyConnection GetProxyConnection(ProxyEndpoint endpoint)
        {
            return proxies!.GetOrAdd(endpoint, e => proxyConnectionFactory.CreateProxyConnection(e));
        }

        public void Dispose()
        {
            if (proxies != null)
            {
                foreach (var proxy in proxies!)
                {
                    try
                    {
                        proxies.TryRemove(proxy.Key, out _);
                        proxy.Value.Dispose();
                    }
                    catch
                    {
                    }
                }

                proxies = null;
            }
        }
    }
}