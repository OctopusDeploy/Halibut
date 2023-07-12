using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Halibut.TestProxy
{
    public static class TcpListenerHelpers
    {
        static readonly Regex ListenParseRegex = new("(?<hostname>[\\S]+):(?<port>\\d+)", RegexOptions.Compiled);

        public static async Task<TcpListener> GetTcpListener(string listenEndpoint)
        {
            if (TryParse(listenEndpoint, out var ipEndPoint))
            {
                return new TcpListener(ipEndPoint);
            }
            else
            {
                var match = ListenParseRegex.Match(listenEndpoint);
                if (match.Success)
                {
                    var hostname = match.Groups["hostname"].Value;
                    var host = await Dns.GetHostEntryAsync(hostname);

                    if (host.AddressList.Length == 0)
                        throw new InvalidOperationException($"Host '{hostname}' could not be resolved to an IP address");

                    return new TcpListener(host.AddressList.First(), int.Parse(match.Groups["port"].Value));
                }
            }

            throw new ArgumentException($"Listening endpoint '{listenEndpoint}' could not be parsed");
        }

        // Taken from net6.0 and adapted to support net48
        public static bool TryParse(string s, out IPEndPoint? result)
        {
            int addressLength = s.Length;  // If there's no port then send the entire string to the address parser
            int lastColonPos = s.LastIndexOf(':');

            // Look to see if this is an IPv6 address with a port.
            if (lastColonPos > 0)
            {
                if (s[lastColonPos - 1] == ']')
                {
                    addressLength = lastColonPos;
                }
                // Look to see if this is IPv4 with a port (IPv6 will have another colon)
                else if (s.Substring(0, lastColonPos).LastIndexOf(':') == -1)
                {
                    addressLength = lastColonPos;
                }
            }

            if (IPAddress.TryParse(s.Substring(0, addressLength), out IPAddress? address))
            {
                uint port = 0;
                if (addressLength == s.Length ||
                    (uint.TryParse(s.Substring(addressLength + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= 0x0000FFFF))

                {
                    result = new IPEndPoint(address, (int)port);
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}