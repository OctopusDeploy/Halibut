using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    [TestTimeout(timeoutInSeconds: 20)]
    public class NetworkTimeoutStreamFixture : BaseTest
    {
        [Test]
        public async Task ReadShouldPassThrough()
        {
            var (disposables, sut, performListenerWrite) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                await performListenerWrite("Test");

                var buffer = new byte[19];
                var readBytes = sut.Read(buffer, 0, 19);
                var readData = Encoding.UTF8.GetString(buffer, 0, readBytes);
                
                Assert.AreEqual("Test", readData);
            }
        }
        
        [Test]
        public async Task ReadAsyncShouldPassThrough()
        {
            var (disposables, sut, performListenerWrite) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                await performListenerWrite("Test");

                var buffer = new byte[19];
                var readBytes = await sut.ReadAsync(buffer, 0, 19, CancellationToken);
                var readData = Encoding.UTF8.GetString(buffer, 0, readBytes);
                
                Assert.AreEqual("Test", readData);
            }
        }

        [Test]
        public async Task ReadAsyncShouldCancel()
        {
            using (var readTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var (disposables, sut, _) = await BuildTcpClientAndTcpListener(CancellationToken);

                using (disposables)
                {
                    // Ensure the timeouts are not used
                    sut.WriteTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;
                    sut.ReadTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;
                    
                    var stopWatch = Stopwatch.StartNew();
                    
                    var actualException = await Try.CatchingError(async () => await sut.ReadAsync(new byte[19], 0, 19, readTokenSource.Token));
                    
                    stopWatch.Stop();

                    actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>();
                    actualException!.Message.Should().Be("The ReadAsync operation was cancelled.");

                    stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
                }
            }
        }

        [Test]
        public async Task ReadAsyncShouldTimeout_AndLookLikeANetworkTimeoutException()
        {
            var (disposables, sut, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                // Ensure the correct timeout is used
                sut.WriteTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;
                sut.ReadTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

                var stopWatch = Stopwatch.StartNew();

                var actualException = await Try.CatchingError(async () => await sut.ReadAsync(new byte[19], 0, 19, CancellationToken));
                
                stopWatch.Stop();

                actualException.Should().NotBeNull().And.BeOfType<IOException>();
                actualException!.Message.Should().Be("Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");

                stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
            }
        }

        [Test]
        public async Task WriteShouldPassThrough()
        {
            string? readData = null;
            
            var (disposables, sut, _) = await BuildTcpClientAndTcpListener(
                CancellationToken,
                onListenerRead: async data =>
                {
                    await Task.CompletedTask;
                    readData += data;
                });

            using (disposables)
            {
                var buffer = Encoding.UTF8.GetBytes("Test");
                sut.Write(buffer, 0, buffer.Length);

                while ((readData?.Length ?? 0) < 4 && !CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(10, CancellationToken);
                }

                Assert.AreEqual("Test", readData);
            }
        }
        
        [Test]
        public async Task WriteAsyncShouldPassThrough()
        {
            string? readData = null;
            
            var (disposables, sut, _) = await BuildTcpClientAndTcpListener(
                CancellationToken,
                onListenerRead: async data =>
                {
                    await Task.CompletedTask;
                    readData += data;
                });

            using (disposables)
            {
                var buffer = Encoding.UTF8.GetBytes("Test");
                await sut.WriteAsync(buffer, 0, buffer.Length, CancellationToken);

                while ((readData?.Length ?? 0) < 4 && !CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(10, CancellationToken);
                }

                Assert.AreEqual("Test", readData);
            }
        }

        [Test]
        public async Task WriteAsyncShouldCancel()
        {
            using (var writeTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var (disposables, sut, _) = await BuildTcpClientAndTcpListener(
                    CancellationToken,
                    onListenerRead: async _ => await DelayForeverToTryAndDelayWriting(CancellationToken));

                using (disposables)
                {
                    // Ensure the timeouts are not used
                    sut.WriteTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;
                    sut.ReadTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

                    var data = new byte[655360];
                    var r = new Random();
                    r.NextBytes(data);

                    var stopWatch = Stopwatch.StartNew();
                    
                    // Brute force attempt to get the Write to be slow
                    var actualException = await Try.RunTillExceptionOrCancellation(
                        async () => await sut.WriteAsync(data, 0, data.Length, writeTokenSource.Token),
                        CancellationToken);
                    
                    stopWatch.Stop();

                    actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>();
                    actualException!.Message.Should().Be("The WriteAsync operation was cancelled.");

                    stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
                }
            }
        }

        [Test]
        public async Task WriteAsyncShouldTimeout_AndLookLikeANetworkTimeoutException()
        {
            var (disposables, sut, _) = await BuildTcpClientAndTcpListener(
                CancellationToken, 
                onListenerRead: async _ => await DelayForeverToTryAndDelayWriting(CancellationToken));

            using (disposables)
            {
                sut.WriteTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
                // Ensure the correct timeout is used
                sut.ReadTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

                var data = new byte[655360];
                var r = new Random();
                r.NextBytes(data);

                var stopWatch = Stopwatch.StartNew();

                // Brute force attempt to get the Write to be slow
                var actualException = await Try.RunTillExceptionOrCancellation(
                    async () => await sut.WriteAsync(data, 0, data.Length, CancellationToken), 
                    CancellationToken);

                stopWatch.Stop();

                actualException.Should().NotBeNull().And.BeOfType<IOException>();
                actualException!.Message.Should().Be("Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");

                stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
            }
        }

        [Test]
        public async Task CloseShouldPassThrough()
        {
            var (disposables, sut, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                sut.Close();

                Action read = () => sut.Read(new byte[1], 0, 1);
                read.Should().Throw<ObjectDisposedException>("Because the stream is closed");
            }
        }

        [Test]
        public async Task DisposeShouldPassThrough()
        {
            var (disposables, sut, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                sut.Dispose();

                Action read = () => sut.Read(new byte[1], 0, 1);
                read.Should().Throw<ObjectDisposedException>("Because the stream is closed");
            }
        }

        static async Task DelayForeverToTryAndDelayWriting(CancellationToken cancellationToken)
        {
            await Task.Delay(-1, cancellationToken);
        }

        async Task<(IDisposable Disposables, Stream SystemUnderTest, Func<string, Task> PerformServiceWriteFunc)> BuildTcpClientAndTcpListener(
            CancellationToken cancellationToken, 
            Func<string, Task>? onListenerRead = null)
        {
            Func<string, Task>? performServiceWriteFunc = null;
            var disposableCollection = new DisposableCollection();

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
            disposableCollection.Add(clientStream);

            var sut = new NetworkTimeoutStream(clientStream);
            disposableCollection.Add(sut);

            while (performServiceWriteFunc == null)
            { 
                await Task.Delay(10, cancellationToken);
            }

            return (disposableCollection, sut, performServiceWriteFunc!);
        }
    }
}
