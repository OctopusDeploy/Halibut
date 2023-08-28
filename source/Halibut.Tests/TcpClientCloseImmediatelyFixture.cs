using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Transport.Streams;
using Halibut.Tests.Util;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TcpClientCloseImmediatelyFixture : BaseTest
    {
        [Test]
        public async Task DoesNotWait()
        {
            var client = await GetTcpClientInWritingBlockedState();

            Logger.Information("Writer has blocked, closing...");
            var stopWatch = Stopwatch.StartNew();
            client.CloseImmediately();
            stopWatch.Stop();
            Logger.Information($"Close completed, duration: {stopWatch.Elapsed.TotalMilliseconds}ms");

            stopWatch.ElapsedMilliseconds.Should().BeLessThan(10);
        }

        [Test]
        public async Task CanBeCalledMultipleTimes()
        {
            var client = await GetTcpClientInWritingBlockedState();

            Logger.Information("Writer has blocked, closing...");
            client.CloseImmediately();
            Logger.Information($"Close completed");

            // Wait a little, to ensure any behind-the-scenes clean up has completed
            await Task.Delay(1000);

            // Try closing again
            Logger.Information("Closing again...");
            client.CloseImmediately();
            Logger.Information("Close completed");
        }

        async Task<TcpClient> GetTcpClientInWritingBlockedState()
        {
            using var writeTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var (sut, client) = await BuildTcpClientAndTcpListener(
                CancellationToken,
                onListenerRead: async _ => await DelayForeverToTryAndDelayWriting(CancellationToken));

            // Ensure the timeouts are not used
            sut.WriteTimeout = (int)TimeSpan.FromSeconds(240).TotalMilliseconds;
            sut.ReadTimeout = (int)TimeSpan.FromSeconds(240).TotalMilliseconds;

            var data = new byte[655360];
            var r = new Random();
            r.NextBytes(data);

            var writerTimedOut = new ManualResetEventSlim(false);

            // Brute force attempt to get the Write to be slow
            Logger.Information("Start writing");
            _ = Task.Run(() =>
            {
                while (true)
                {
                    Logger.Information("WRITER: Start");
                    var writeCompleted = sut.WriteToStream(StreamWriteMethod.WriteAsync, data, 0, data.Length, writeTokenSource.Token).Wait(TimeSpan.FromSeconds(1));
                    if (!writeCompleted)
                    {
                        Logger.Information("WRITER: Timed out");
                        writerTimedOut.Set();
                        return Task.CompletedTask;
                    }

                    Logger.Information("WRITER: Finished");
                }
            }, writeTokenSource.Token);

            Logger.Information("Wait for writer to block...");
            writerTimedOut.Wait(CancellationToken);

            return client;
        }

        async Task<(Stream SystemUnderTest, TcpClient Client)> BuildTcpClientAndTcpListener(
            CancellationToken cancellationToken,
            Func<string, Task>? onListenerRead = null)
        {
            Func<string, Task>? performServiceWriteFunc = null;

            var service = new TcpListener(IPAddress.Loopback, 0);
            service.Start();

            var _ = Task.Run(async () =>
            {
                using var serviceTcpClient = await service.AcceptTcpClientAsync();
                serviceTcpClient.ReceiveBufferSize = 10;
                serviceTcpClient.SendBufferSize = 10;

                using var serviceStream = serviceTcpClient.GetStream();
                performServiceWriteFunc = async data => await serviceStream.WriteAsync(Encoding.UTF8.GetBytes(data), 0, data.Length, cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var buffer = new byte[19];
                    var readBytes = await serviceStream.ReadAsync(buffer, 0, 19, cancellationToken);

                    var readData = Encoding.UTF8.GetString(buffer, 0, readBytes);
                    if (onListenerRead != null)
                    {
                        await onListenerRead(readData);
                    }
                }
            }, cancellationToken);

            var client = new TcpClient();
            client.ReceiveBufferSize = 10;
            client.SendBufferSize = 10;

            await client.ConnectAsync("localhost", ((IPEndPoint)service.LocalEndpoint).Port);

            var clientStream = client.GetStream();
            while (performServiceWriteFunc == null)
            {
                await Task.Delay(10, cancellationToken);
            }

            return (clientStream, client);
        }

        static async Task DelayForeverToTryAndDelayWriting(CancellationToken cancellationToken)
        {
            await Task.Delay(-1, cancellationToken);
        }
    }
}
