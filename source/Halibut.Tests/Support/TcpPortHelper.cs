using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Support
{
    class TcpPortHelper
    {
        internal static int FindFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
        
        static SemaphoreSlim semaphore = new(1,1);
        internal static async Task<IDisposable> WaitForLock(CancellationToken cancellationToken)
        {
            semaphore.WaitAsync(cancellationToken);

            return new EnteredSemaphoreSlim(semaphore);
        }

        class EnteredSemaphoreSlim : IDisposable
        {
            bool released;
            SemaphoreSlim semaphoreSlim;
            readonly object releaseLock = new();

            public EnteredSemaphoreSlim(SemaphoreSlim semaphoreSlim)
            {
                this.semaphoreSlim = semaphoreSlim;
            }

            public void Dispose()
            {
                lock (releaseLock)
                {
                    if (!released)
                    {
                        released = true;
                        semaphoreSlim.Release();
                        semaphoreSlim = null;
                    }
                }
            }
        }
    }
}
