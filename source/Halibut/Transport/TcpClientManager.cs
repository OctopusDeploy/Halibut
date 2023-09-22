using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Halibut.Transport
{
    class TcpClientManager : IDisposable
    {
        readonly Dictionary<string, HashSet<TcpClient>> activeClients = new();

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

        // TODO - ASYNC ME UP - Should be obsolete but in use and needs an async chain
        public void Disconnect(string thumbprint)
        {
            lock (activeClients)
            {
                if (activeClients.TryGetValue(thumbprint, out var tcpClients))
                {
                    foreach (var client in tcpClients)
                    {
                        client.CloseImmediately();
                    }
                }
                activeClients.Remove(thumbprint);
            }
        }

        static readonly TcpClient[] NoClients = Array.Empty<TcpClient>();
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

        public void RemoveClient(TcpClient client)
        {
            lock (activeClients)
            {
                foreach(var thumbprintClientsPair in activeClients)
                {
                    if (thumbprintClientsPair.Value.Contains(client))
                        thumbprintClientsPair.Value.Remove(client);
                }

                var thumbprintsWithNoClients = activeClients
                    .Where(x => x.Value.Count == 0)
                    .Select(x => x.Key)
                    .ToArray();
                foreach (var thumbprint in thumbprintsWithNoClients)
                    activeClients.Remove(thumbprint);
            }
        }

        public void Dispose()
        {
            var clients = activeClients?.ToArray();
            activeClients?.Clear();

            if (clients == null || !clients.Any())
            {
                return;
            }
            
            foreach (var client in clients)
            {
                if (client.Value?.Any() == true)
                {
                    var tcpClients = client.Value.ToArray();

                    foreach (var tcpClient in tcpClients)
                    {
                        tcpClient?.CloseImmediately();
                        tcpClient?.Dispose();
                    }
                }
            }
        }
    }
}
