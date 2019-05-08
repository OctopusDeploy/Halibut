using System;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public class ConnectionManager : IDisposable
    {
        readonly ConnectionPool<ServiceEndPoint, IConnection> pool = new ConnectionPool<ServiceEndPoint, IConnection>();

        public IConnection AcquireConnection(IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log)
        {
            var connection = pool.Take(serviceEndpoint);
            return connection ?? connectionFactory.EstablishNewConnection(serviceEndpoint, log);
        }

        public void ReleaseConnection(ServiceEndPoint serviceEndpoint, IConnection connection)
        {
            pool.Return(serviceEndpoint, connection);
        }

        public void ClearPooledConnections(ServiceEndPoint serviceEndPoint, ILog log)
        {
            pool.Clear(serviceEndPoint, log);
        }

        public void Disconnect(ServiceEndPoint serviceEndPoint, ILog log)
        {
            ClearPooledConnections(serviceEndPoint, log);
        }

        public void Dispose()
        {
            pool.Dispose();
        }
    }
}