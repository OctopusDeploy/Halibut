// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#if NET8_0_OR_GREATER

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Proxy.Exceptions;
using Halibut.Transport.Streams;

namespace Halibut.Transport.Proxy
{
    public class SocksProxyClient : IProxyClient
    {
        readonly ILog log;
        readonly string? proxyUsername;
        readonly string? proxyPassword;
        readonly IStreamFactory streamFactory;
        public string ProxyHost { get; set; }
        public int ProxyPort { get; set; }

        public TcpClient? TcpClient { get; set; }
        public string ProxyName => "SOCKS";

        Func<TcpClient>? tcpClientFactory;
        readonly Uri proxyUri;

        static readonly Lazy<Func<Stream, string, int, Uri, ICredentials?, bool, CancellationToken, ValueTask>> EstablishSocksTunnel = new(() =>
        {
            var socksHelperType = typeof(System.Net.Http.HttpClient).Assembly.GetType("System.Net.Http.SocksHelper");
            if (socksHelperType == null) throw new InvalidOperationException("Could not find System.Net.Http.SocksHelper type.");

            var method = socksHelperType.GetMethod("EstablishSocksTunnelAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)  throw new InvalidOperationException("Could not find EstablishSocksTunnelAsync method on SocksHelper.");

            return (Func<Stream, string, int, Uri, ICredentials?, bool, CancellationToken, ValueTask>) Delegate.CreateDelegate(typeof(Func<Stream, string, int, Uri, ICredentials?, bool, CancellationToken, ValueTask>), method); });

        public SocksProxyClient(ILog log, string proxyHost, int proxyPort, string? proxyUsername, string? proxyPassword, IStreamFactory streamFactory)
        {
            this.log = log;
            this.proxyUsername = proxyUsername;
            this.proxyPassword = proxyPassword;
            this.streamFactory = streamFactory;
            ProxyHost = proxyHost;
            ProxyPort = proxyPort;

            // ToDo: Use the correct protocol scheme for difference version of SOCKS
            proxyUri = new Uri($"socks5://{proxyHost}:{proxyPort}");
        }

        public IProxyClient WithTcpClientFactory(Func<TcpClient> tcpClientfactory)
        {
            tcpClientFactory = tcpClientfactory;
            return this;
        }

        public async Task<TcpClient> CreateConnectionAsync(string destinationHost, int destinationPort, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                // if we have no connection, create one
                if (TcpClient == null)
                {
                    if (string.IsNullOrEmpty(ProxyHost))
                        throw new ProxyException("ProxyHost property must contain a value", false);

                    if (ProxyPort <= 0 || ProxyPort > 65535)
                        throw new ProxyException("ProxyPort value must be greater than zero and less than 65535", false);

                    if(ProxyHost.Contains("://"))
                        throw new ProxyException("The proxy's hostname cannot contain a protocol prefix (eg http://)", false);

                    TcpClient = tcpClientFactory!();

                    // attempt to open the connection
                    log.Write(EventType.Diagnostic, "Connecting to proxy at {0}:{1}", ProxyHost, ProxyPort);
                    await TcpClient.ConnectWithTimeoutAsync(ProxyHost, ProxyPort, timeout, cancellationToken);
                    log.Write(EventType.Diagnostic, "Connected to proxy at {0}:{1}", ProxyHost, ProxyPort);
                }

                var stream = streamFactory.CreateStream(TcpClient!);
                await EstablishSocksTunnel.Value(stream, destinationHost, destinationPort, proxyUri, GetCredentials(), true, cancellationToken);

                // return the open proxied tcp client object to the caller for normal use
                return TcpClient;
            }
            catch (AggregateException ae)
            {
                var se = ae.InnerExceptions.OfType<SocketException>().FirstOrDefault();
                if (se != null)
                    throw new ProxyException($"Connection to proxy host {ProxyHost} on port {ProxyPort} failed: {se.Message}", se, true);

                throw;
            }
            catch (SocketException ex)
            {
                throw new ProxyException($"Connection to proxy host {ProxyHost} on port {ProxyPort} failed: {ex.Message}", ex, true);
            }
        }

        ICredentials? GetCredentials()
        {
            if (proxyUsername != null && proxyPassword != null) return new NetworkCredential(proxyUsername, proxyPassword);
            return null;
        }
    }
}

#endif