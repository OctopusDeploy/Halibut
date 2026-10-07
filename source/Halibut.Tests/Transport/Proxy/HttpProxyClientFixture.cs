using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Transport;
using Halibut.Transport.Proxy;
using Halibut.Transport.Proxy.Exceptions;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Proxy
{
    [NonParallelizable]
    public class HttpProxyClientFixture : BaseTest
    {
        [TestCase("", TestName = "ProxyClosesWithoutResponding")]
        [TestCase("HTTP/1.1 200 Connection established\r\n", TestName = "ProxyClosesAfterPartialResponse")]
        public async Task CreateConnection_WhenProxyClosesBeforeFullResponse_ShouldFailPromptly(string partialResponse)
        {
            // A "proxy" that accepts the TCP connection, optionally writes part of a response, then closes.
            var listener = new TcpListener(IPAddress.IPv6Any, 0);
            listener.Server.DualMode = true;
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = Task.Run(async () =>
            {
                using var accepted = await listener.AcceptTcpClientAsync();
                // Wait for the CONNECT request so the client is inside its response read loop before we close.
                var buffer = new byte[4096];
                var request = new StringBuilder();
                while (!request.ToString().Contains("\r\n\r\n"))
                {
                    var read = await accepted.GetStream().ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }
                await Task.Delay(500);
                var bytes = Encoding.ASCII.GetBytes(partialResponse);
                if (bytes.Length > 0) await accepted.GetStream().WriteAsync(bytes, 0, bytes.Length);
                accepted.Client.Shutdown(SocketShutdown.Both);
            });

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var proxyClient = new HttpProxyClient(new InMemoryConnectionLog("test"), "localhost", port, null, null, new StreamFactory())
                    .WithTcpClientFactory(() => TcpConnectionFactory.CreateTcpClientAsync(HalibutTimeoutsAndLimits.RecommendedValues()));

                var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
                var sw = Stopwatch.StartNew();
                Exception? caught = null;
                try
                {
                    await proxyClient.CreateConnectionAsync("example.com", 443, TimeSpan.FromSeconds(5), cts.Token);
                }
                catch (Exception e)
                {
                    caught = e;
                }

                var cpu = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
                Logger.Information("Took {Elapsed}, CPU {Cpu}ms, exception: {Exception}", sw.Elapsed, cpu.TotalMilliseconds, caught?.ToString());

                caught.Should().NotBeNull();
                caught.Should().NotBeOfType<OperationCanceledException>("the client should detect the proxy closed the connection rather than spin until cancelled");
                caught.Should().BeOfType<ProxyException>();
                sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
            }
            finally
            {
                listener.Stop();
                await acceptTask;
            }
        }
    }
}
