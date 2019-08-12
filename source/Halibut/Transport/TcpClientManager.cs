using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Halibut.Transport
{
    class TcpClientManager
    {
        readonly Dictionary<string, HashSet<TcpClient>> activeClients = new Dictionary<string, HashSet<TcpClient>>();

        public void AddActiveClient(string thumbprint, TcpClient client)
        {
            lock (activeClients)
            {
                if (activeClients.TryGetValue(thumbprint, out var tcpClients))
                {
                    tcpClients.RemoveWhere(c => !c.Connected);
                    tcpClients.Add(client);
                }
                else
                {
                    tcpClients = new HashSet<TcpClient> {client};
                    activeClients.Add(thumbprint, tcpClients);
                }
            }
        }

        public void Disconnect(string thumbprint)
        {
            lock (activeClients)
            {
                if (activeClients.TryGetValue(thumbprint, out var tcpClients))
                {
                    foreach (var client in tcpClients)
                    {
                        client.Close();
                    }
                }
                activeClients.Remove(thumbprint);
            }
        }

        static readonly TcpClient[] NoClients = new TcpClient[0];
        public IReadOnlyCollection<TcpClient> GetActiveClients(string thumbprint)
        {
            lock (activeClients)
            {
                if (activeClients.TryGetValue(thumbprint, out var value))
                {
                    return value.ToArray();
                }
            }

            return NoClients;
        }

        public void RemoveDisconnectedClient(TcpClient client)
        {
            lock (activeClients)
            {
                foreach(var activeClient in activeClients)
                {
                    if (activeClient.Value.Contains(client))
                        activeClient.Value.Remove(client);
                }

                var inactiveThumbprints = activeClients
                    .Where(x => x.Value.Count == 0)
                    .Select(x => x.Key)
                    .ToArray();
                foreach (var inactiveThumbprint in inactiveThumbprints)
                    activeClients.Remove(inactiveThumbprint);
            }
        }
    }
}
