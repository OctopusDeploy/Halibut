using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Transport;
using Halibut.Transport.Proxy;
using Halibut.Transport.Proxy.Exceptions;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Proxy
{
    public class HttpProxyClientFixture : BaseTest
    {
        [TestCase("")]
        [TestCase("HTTP/1.1 200 Connection established\r\n")]
        public async Task CreateConnection_WhenProxyHangsUpBeforeCompletingConnectResponse_Throws(string partialResponse)
        {
            await using var proxy = FakeProxyThatHangsUpAfterReceivingConnectRequest.Start(partialResponse);

            var proxyClient = new HttpProxyClient(new InMemoryConnectionLog("test"), "localhost", proxy.Port, null, null, new StreamFactory())
                .WithTcpClientFactory(() => TcpConnectionFactory.CreateTcpClientAsync(HalibutTimeoutsAndLimits.RecommendedValues()));

            // This used to hang forever.
            var createConnection = proxyClient.CreateConnectionAsync("example.com", 443, TimeSpan.FromSeconds(5), CancellationToken);
            (await Task.WhenAny(createConnection, Task.Delay(TimeSpan.FromSeconds(10), CancellationToken)))
                .Should().BeSameAs(createConnection, "CreateConnectionAsync should return once the proxy hangs up");

            await AssertException.Throws<ProxyException>(createConnection);
        }

        /// <summary>
        /// A fake HTTP proxy that accepts one connection, waits for the CONNECT request,
        /// sends <c>partialResponse</c> (never the full response), then hangs up.
        /// </summary>
        class FakeProxyThatHangsUpAfterReceivingConnectRequest : IAsyncDisposable
        {
            readonly TcpListener listener;
            readonly Task acceptTask;
            TcpClient? accepted;

            FakeProxyThatHangsUpAfterReceivingConnectRequest(string partialResponse)
            {
                listener = new TcpListener(IPAddress.IPv6Any, 0);
                listener.Server.DualMode = true;
                listener.Start();
                acceptTask = HangUpAfterConnectRequest(partialResponse);
            }

            public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

            public static FakeProxyThatHangsUpAfterReceivingConnectRequest Start(string partialResponse) => new(partialResponse);

            async Task HangUpAfterConnectRequest(string partialResponse)
            {
                accepted = await listener.AcceptTcpClientAsync();
                var stream = accepted.GetStream();

                var buffer = new byte[4096];
                var request = "";
                while (!request.Contains("\r\n\r\n"))
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) return;
                    request += Encoding.ASCII.GetString(buffer, 0, read);
                }

                var response = Encoding.ASCII.GetBytes(partialResponse);
                await stream.WriteAsync(response, 0, response.Length);

                // Let the client start reading the response before hanging up.
                await Task.Delay(TimeSpan.FromSeconds(1));

                // Shutdown rather than Dispose: the client then reads EOF (0 bytes) forever, which is what triggered the spin.
                accepted.Client.Shutdown(SocketShutdown.Both);
            }

            public async ValueTask DisposeAsync()
            {
                listener.Stop();
                await acceptTask;
                accepted?.Dispose();
            }
        }
    }
}
