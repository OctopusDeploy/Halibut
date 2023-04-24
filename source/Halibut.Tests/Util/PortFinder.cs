using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace Halibut.Tests.Util
{
    /// <summary>
    ///     <para>
    /// Thread safe provider of a TCP port that is known to be free at one point in time during the
    /// lookup. No guarantees whatsoever that the port will still be free when you go to use it.
    ///     </para>
    ///     <para>
    /// The use of an incrementing counter guarantees that if you've found a port via this instance
    /// then you won't be given that port again, even if it's not in use.
    ///     </para>
    /// </summary>
    public class FreeTcpPortFinder
    {
        static readonly object mutex = new object();
        static volatile int counter = 10950; 

        public static int Find()
        {
            lock (mutex)
            {
                var tcpPortsInUse = new HashSet<int>(
                    IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Select(endpoint => endpoint.Port)
                );

                var nextAvailableTcpPortOrNull =
                    Enumerable.Range(counter, IPEndPoint.MaxPort - counter)
                        .FirstOrDefault(candidatePort => !tcpPortsInUse.Contains(candidatePort));

                if (nextAvailableTcpPortOrNull == 0)
                    throw new InvalidOperationException(
                        $"Reached the maximum valid TCP port number ({IPEndPoint.MaxPort}) without finding a free port");

                // Guarantee the next call won't return the same port as the current call
                counter = nextAvailableTcpPortOrNull + 1;

                return nextAvailableTcpPortOrNull;
            }
        }
    }
}