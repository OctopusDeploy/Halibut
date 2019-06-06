using System;
using System.Collections.Generic;
using Halibut.Transport;

namespace Halibut.ServiceModel
{
    class PollingClientCollection
    {
        readonly List<IPollingClient> pollingClients = new List<IPollingClient>();
        readonly object sync = new object();

        public void Add(IPollingClient pollingClient)
        {
            lock (sync)
            {
                pollingClients.Add(pollingClient);
            }

            pollingClient.Start();
        }

        public void Dispose()
        {
            lock (sync)
            {
                foreach (var worker in pollingClients)
                {
                    worker.Dispose();
                }

                pollingClients.Clear();
            }
        }
    }
}