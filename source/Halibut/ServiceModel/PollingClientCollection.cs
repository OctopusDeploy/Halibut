using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Halibut.Transport;

namespace Halibut.ServiceModel
{
    public class PollingClientCollection
    {
        readonly List<IPollingClient> pollingClients = new List<IPollingClient>();
        readonly object sync = new object();

        public void Add(PollingClient pollingClient)
        {
            lock (sync)
            {
                pollingClients.Add(pollingClient);
            }

            pollingClient.Start();
        }

        public async Task Stop()
        {
            IPollingClient[] pollingClientsCopy;

            lock (sync)
            {
                pollingClientsCopy = pollingClients.ToArray();
            }

            foreach (var worker in pollingClientsCopy)
            {
                await worker.Stop().ConfigureAwait(false);
            }

            lock (sync)
            {
                pollingClients.Clear();
            }
        }
    }
}