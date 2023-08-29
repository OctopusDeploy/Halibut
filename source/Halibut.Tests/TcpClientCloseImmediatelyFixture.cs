using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Transport.Streams;
using Halibut.Tests.Util;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TcpClientCloseImmediatelyFixture : BaseTest
    {
        [Test]
        public async Task DoesNotWaitNew()
        {
            var (client, clientStream, serverStream) = await BuildTcpClientAndTcpListener(CancellationToken);

            await WriteToStreamUntilBlocked(clientStream);

            Logger.Information("Writer has blocked, closing...");
            var stopWatch = Stopwatch.StartNew();
            client.CloseImmediately();
            stopWatch.Stop();
            Logger.Information($"Close completed, duration: {stopWatch.Elapsed.TotalMilliseconds}ms");
            
            stopWatch.ElapsedMilliseconds.Should().BeLessThan(10);

            byte[] received = new byte[65536];
            await AssertAsync.Throws<IOException>(async () => _ = await serverStream.ReadAsync(received, 0, received.Length, CancellationToken));
        }

        [Test]
        public async Task CanBeCalledMultipleTimes()
        {
            var (client, clientStream, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            await WriteToStreamUntilBlocked(clientStream);

            Logger.Information("Writer has blocked, closing...");
            client.Close();
            Logger.Information($"Close completed");

            // Wait a little, to ensure any behind-the-scenes clean up has completed
            await Task.Delay(1000);

            // Try closing again
            Logger.Information("Closing again...");
            client.CloseImmediately();
            Logger.Information("Close completed");
        }

        async Task<(TcpClient Client, Stream ClientStream, Stream ServerStream)> BuildTcpClientAndTcpListener(CancellationToken cancellationToken)
        {
            var server = new TcpListener(IPAddress.Loopback, 0);
            server.Start();

            using var semaphore = new SemaphoreSlim(0, 1);
            Stream? serverStream = null;

            var _ = Task.Run(async () =>
            {
                using var serviceTcpClient = await server.AcceptTcpClientAsync();
                serviceTcpClient.ReceiveBufferSize = 10;
                serviceTcpClient.SendBufferSize = 10;

                using var serviceStream = serviceTcpClient.GetStream();
                serverStream = serviceStream;
                semaphore.Release();

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }
            }, cancellationToken);

            var client = new TcpClient();
            client.ReceiveBufferSize = 10;
            client.SendBufferSize = 10;

            await client.ConnectAsync("localhost", ((IPEndPoint)server.LocalEndpoint).Port);

            var clientStream = client.GetStream();
            await semaphore.WaitAsync(cancellationToken);

            return (client, clientStream, serverStream!);
        }
        
        async Task WriteToStreamUntilBlocked(Stream stream)
        {
            var data = new byte[655360];
            var r = new Random();
            r.NextBytes(data);

            while (true)
            {
                var timeoutTask = Task.Delay(1000, CancellationToken);
                var writingTask = stream.WriteToStream(StreamWriteMethod.WriteAsync, data, 0, data.Length, CancellationToken);
                Logger.Information("Start writing");
                var completedTask = await Task.WhenAny(writingTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    Logger.Information("Writing operation timed out");
                    return;
                }
                
                Logger.Information("Finished writing");
            }
        }
    }
}
