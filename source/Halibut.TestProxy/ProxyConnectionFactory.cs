using Microsoft.Extensions.Logging;

namespace Halibut.TestProxy
{
    interface IProxyConnectionFactory
    {
        IProxyConnection CreateProxyConnection(ProxyEndpoint endpoint);
    }

    class ProxyConnectionFactory : IProxyConnectionFactory
    {
        readonly ILoggerFactory loggerFactory;

        public ProxyConnectionFactory(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
        }

        public IProxyConnection CreateProxyConnection(ProxyEndpoint endpoint)
        {
            return new ProxyConnection(
                endpoint,
                loggerFactory.CreateLogger<ProxyConnection>());
        }
    }
}