using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Halibut.TestProxy
{
    interface IProxyConnectionService
    {
        IProxyConnection GetProxyConnection(ProxyEndpoint endpoint);
    }

    class ProxyConnectionService : IProxyConnectionService, IHostedService
    {
        readonly IProxyConnectionFactory proxyConnectionFactory;
        readonly ILogger<ProxyConnectionService> logger;
        readonly ConcurrentDictionary<ProxyEndpoint, IProxyConnection> proxies = new();

        public ProxyConnectionService(
            IProxyConnectionFactory proxyConnectionFactory,
            ILogger<ProxyConnectionService> logger)
        {
            this.proxyConnectionFactory = proxyConnectionFactory;
            this.logger = logger;
        }

        public IProxyConnection GetProxyConnection(ProxyEndpoint endpoint)
        {
            return proxies.GetOrAdd(endpoint, e => proxyConnectionFactory.CreateProxyConnection(e));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Closing proxy connections");

            try
            {
                await Task.WhenAll(
                    proxies.Values.ToList().Select(async (proxyConnection) =>
                    {
                        proxyConnection.Dispose();
                    }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error has occurred closing proxy connections");
            }
        }
    }
}