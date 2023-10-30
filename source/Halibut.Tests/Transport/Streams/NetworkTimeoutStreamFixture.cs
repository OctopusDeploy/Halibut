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
using Halibut.Tests.Util;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    [TestTimeout(timeoutInSeconds: 20)]
    public class NetworkTimeoutStreamFixture : BaseTest
    {
        [Test]
        [StreamReadMethodTestCase]
        public async Task ReadCalls_ShouldPassThrough(StreamReadMethod streamReadMethod)
        {
            var (disposables, sut, _, _, performListenerWrite) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                await performListenerWrite("Test");

                if (streamReadMethod == StreamReadMethod.ReadByte)
                {
                    var readByte = sut.ReadByte();
                
                    Assert.AreEqual("Test".GetBytesUtf8()[0], readByte);
                }
                else
                {
                    var buffer = new byte[19];
                    var readBytes = await sut.ReadFromStream(streamReadMethod, buffer, 0, 19, CancellationToken);
                    var readData = Encoding.UTF8.GetString(buffer, 0, readBytes);

                    Assert.AreEqual("Test", readData);
                }
            }
        }

        [Test]
        [StreamReadMethodTestCase]
        public async Task ReadCalls_ShouldTimeout_AndCloseTheStream_AndThrowExceptionThatLooksLikeANetworkTimeoutException(StreamReadMethod streamReadMethod)
        {
            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                // Ensure the correct timeout is used
                sut.WriteTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;
                sut.ReadTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

                var stopWatch = Stopwatch.StartNew();

                var actualException = await Try.CatchingError(async () => await sut.ReadFromStream(streamReadMethod, new byte[19], 0, 19, CancellationToken));
                
                stopWatch.Stop();

                actualException.Should().NotBeNull().And.BeOfType<IOException>();
                actualException!.Message.Should().ContainAny(
                    "Unable to read data from the transport connection: Connection timed out.",
                    "Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");
                
                stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

                AssertStreamWasClosed(streamReadMethod, callCountingStream);
            }
        }

        [Test]
        [StreamReadMethodTestCase(testSync: false)]
        public async Task ReadAsyncCalls_ShouldCancel(StreamReadMethod streamReadMethod)
        {
            using (var readTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var (disposables, sut, _, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

                using (disposables)
                {
                    // Ensure the timeouts are not used
                    sut.WriteTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;
                    sut.ReadTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

                    var stopWatch = Stopwatch.StartNew();

                    var actualException = await Try.CatchingError(async () => await sut.ReadFromStream(streamReadMethod, new byte[19], 0, 19, readTokenSource.Token));

                    stopWatch.Stop();

                    actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>();
                    actualException!.Message.Should().Be("The ReadAsync operation was cancelled.");

                    stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
                }
            }
        }
        
        [Test]
        [StreamWriteMethodTestCase]
        public async Task WriteCalls_ShouldPassThrough(StreamWriteMethod streamWriteMethod)
        {
            string? readData = null;
            
            var (disposables, sut, _, _, _) = await BuildTcpClientAndTcpListener(
                CancellationToken,
                onListenerRead: async data =>
                {
                    await Task.CompletedTask;
                    readData += data;
                });

            using (disposables)
            {
                var buffer = Encoding.UTF8.GetBytes("Test");
                await sut.WriteToStream(streamWriteMethod, buffer, 0, buffer.Length, CancellationToken);

                if (streamWriteMethod == StreamWriteMethod.WriteByte)
                {
                    while ((readData?.Length ?? 0) < 1 && !CancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(10, CancellationToken);
                    }

                    Assert.AreEqual("Test"[0], readData.ToCharArray()[0]);  
                }
                else
                {
                    while ((readData?.Length ?? 0) < 4 && !CancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(10, CancellationToken);
                    }

                    Assert.AreEqual("Test", readData);   
                }
            }
        }
        
        [Test]
        [StreamWriteMethodTestCase]
        public async Task WriteCalls_ShouldTimeout_AndCloseTheStream_AndThrowExceptionThatLooksLikeANetworkTimeoutException(StreamWriteMethod streamWriteMethod)
        {
            if (streamWriteMethod == StreamWriteMethod.WriteByte)
            {
                Assert.Inconclusive("This test is unable to brute force enough writes for WriteByte to timeout");
            }

            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(
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
                    async () => await sut.WriteToStream(streamWriteMethod, data, 0, data.Length, CancellationToken), 
                    CancellationToken);

                stopWatch.Stop();

                actualException.Should().NotBeNull().And.BeOfType<IOException>();
                actualException!.Message.Should().ContainAny(
                    "Unable to write data to the transport connection: Connection timed out.",
                    "Unable to write data to the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");

                stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
                
                AssertStreamWasClosed(streamWriteMethod, callCountingStream);
            }
        }
        
        [Test]
        [StreamWriteMethodTestCase(testSync: false)]
        public async Task WriteAsyncCalls_ShouldCancel(StreamWriteMethod streamWriteMethod)
        {
            using (var writeTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var (disposables, sut, _, _, _) = await BuildTcpClientAndTcpListener(
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
                        async () => await sut.WriteToStream(streamWriteMethod, data, 0, data.Length, writeTokenSource.Token),
                        CancellationToken);

                    stopWatch.Stop();

                    actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>();
                    actualException!.Message.Should().Be("The WriteAsync operation was cancelled.");

                    stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
                }
            }
        }

        [Test]
        public async Task Close_ShouldPassThrough()
        {
            var (disposables, sut, _, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                sut.Close();

                Action read = () => sut.Read(new byte[1], 0, 1);
                read.Should().Throw<ObjectDisposedException>("Because the stream is closed");
            }
        }

        [Test]
        public async Task Dispose_ShouldPassThrough()
        {
            var (disposables, sut, _, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                sut.Dispose();

                Action read = () => sut.Read(new byte[1], 0, 1);
                read.Should().Throw<ObjectDisposedException>("Because the stream is closed");
            }
        }

        [Test]
        public async Task DisposeAsync_ShouldPassThrough()
        {
            var (disposables, sut, _, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                await sut.DisposeAsync();

                Action read = () => sut.Read(new byte[1], 0, 1);
                read.Should().Throw<ObjectDisposedException>("Because the stream is closed");
            }
        }

        [Test]
        public async Task Flush_ShouldPassThrough()
        {
            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                sut.Flush();
                callCountingStream.FlushCallCount.Should().Be(1);
            }
        }

        [Test]
        public async Task FlushAsync_ShouldPassThrough()
        {
            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                await sut.FlushAsync();
                callCountingStream.FlushAsyncCallCount.Should().Be(1);
            }
        }

        [Test]
        [SyncAndAsync]
        public async Task FlushOrFlushAsync_ShouldTimeout_AndCloseTheStream_AndThrowExceptionThatLooksLikeANetworkTimeoutException(SyncOrAsync syncOrAsync)
        {
            var (disposables, sut, callCountingStream, pausingStream, _) = await BuildTcpClientAndTcpListener(
                CancellationToken, 
                onListenerRead: async _ => await DelayForeverToTryAndDelayWriting(CancellationToken));

            using (disposables)
            {
                sut.WriteTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
                // Ensure the correct timeout is used
                sut.ReadTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

                // NetworkStream implementation of Flush and FlushAsync is a NoOp so pause to make sure our wrapper is working
                pausingStream.PauseUntilTimeout(CancellationToken, pauseDisposeOrClose: false);

                (await AssertAsync.Throws<IOException>(async () =>
                    {
                        switch (syncOrAsync)
                        {
                            case SyncOrAsync.Async:
                                await sut.FlushAsync(CancellationToken);
                                return;
                            case SyncOrAsync.Sync:
                                sut.Flush();
                                return;
                        }
                    }))
                    .And.Message.Should().ContainAny(
                    "Unable to write data to the transport connection: Connection timed out.",
                    "Unable to write data to the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");
                                
                AssertStreamWasClosed(syncOrAsync, callCountingStream);
            }
        }

        [Test]
        public async Task Seek_ShouldPassThrough()
        {
            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                Try.CatchingError(() => sut.Seek(0, SeekOrigin.Begin), _ => { });
                callCountingStream.SeekCallCount.Should().Be(1);
            }
        }

        [Test]
        public async Task SetLength_ShouldPassThrough()
        {
            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                Try.CatchingError(() => sut.SetLength(0), _ => { });
                callCountingStream.SetLengthCallCount.Should().Be(1);
            }
        }

#if NETFRAMEWORK
        [Test]
        public async Task CreateObjRef_ShouldPassThrough()
        {
            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                Try.CatchingError(() => sut.CreateObjRef(typeof(NetworkTimeoutStream)), _ => { });
                callCountingStream.CreateObjRefCallCount.Should().Be(1);
            }
        }

        [Test]
        public async Task InitializeLifetimeService_ShouldPassThrough()
        {
            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                sut.InitializeLifetimeService();
                callCountingStream.InitializeLifetimeServiceCallCount.Should().Be(1);
            }
        }
#endif

        [Test]
        [StreamCopyToMethodTestCase]
        public async Task CopyToCalls_ShouldPassThrough(StreamCopyToMethod streamCopyToMethod)
        {
            using var source = new MemoryStream();
            await using var sut = new NetworkTimeoutStream(source);
            
            var buffer = Encoding.UTF8.GetBytes("Test");
            await source.WriteAsync(buffer, 0, buffer.Length, CancellationToken);
            source.Position = 0;

            var destinationStream = new MemoryStream();
            await sut.CopyToStream(streamCopyToMethod, destinationStream, buffer.Length, CancellationToken);

            sut.Position = 0;
            using var reader = new StreamReader(sut);
            var readData = await reader.ReadToEndAsync();

            Assert.AreEqual("Test", readData);
        }

        [Test]
        [StreamCopyToMethodTestCase]
        public async Task CopyToCalls_ShouldTimeout_AndCloseTheStream_AndThrowExceptionThatLooksLikeANetworkTimeoutException(StreamCopyToMethod streamCopyToMethod)
        {
            using var source = new MemoryStream();
            await using var supportsTimeoutsStream = new SupportsTimeoutsStream(source, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
            await using var pausableStream = new PausingStream(supportsTimeoutsStream);
            await using var callCountingStream = new CallCountingStream(pausableStream);
            var sut = new NetworkTimeoutStream(callCountingStream);
            
            sut.ReadTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
            // Ensure the correct timeout is used
            sut.WriteTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

            var buffer = Encoding.UTF8.GetBytes("Test");
            await source.WriteAsync(buffer, 0, buffer.Length, CancellationToken);
            source.Position = 0;

            var destinationStream = new MemoryStream();
            
            var stopWatch = Stopwatch.StartNew();
            
            pausableStream.PauseUntilTimeout(CancellationToken, pauseDisposeOrClose: false);

            var actualException = await Try.CatchingError(async () => await sut.CopyToStream(streamCopyToMethod, destinationStream, buffer.Length, CancellationToken));

            stopWatch.Stop();

            actualException.Should().NotBeNull().And.BeOfType<IOException>();
            actualException!.Message.Should().ContainAny(
                "Unable to read data from the transport connection: Connection timed out.",
                "Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");

            stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

            AssertStreamWasClosed(streamCopyToMethod, callCountingStream);
        }

        [Test]
        [StreamCopyToMethodTestCase(testSync: false)]
        public async Task CopyAsyncCalls_ShouldCancel(StreamCopyToMethod streamCopyToMethod)
        {
#if NETFRAMEWORK
            if (streamCopyToMethod == StreamCopyToMethod.CopyToAsync)
            {
                Assert.Inconclusive("CopyToAsync in net48 does not accept a cancellation token");
            }
#endif

            using (var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                using var source = new MemoryStream();
                await using var supportsTimeoutsStream = new SupportsTimeoutsStream(source, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
                await using var pausableStream = new PausingStream(supportsTimeoutsStream);
                await using var callCountingStream = new CallCountingStream(pausableStream);
                var sut = new NetworkTimeoutStream(callCountingStream);
            
                sut.ReadTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;
                sut.WriteTimeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

                var buffer = Encoding.UTF8.GetBytes("Test");
                await source.WriteAsync(buffer, 0, buffer.Length, CancellationToken);
                source.Position = 0;

                var destinationStream = new MemoryStream();
            
                var stopWatch = Stopwatch.StartNew();
            
                pausableStream.PauseUntilTimeout(CancellationToken, pauseDisposeOrClose: false);

                var actualException = await Try.CatchingError(async () => await sut.CopyToStream(streamCopyToMethod, destinationStream, buffer.Length, timeoutTokenSource.Token));

                stopWatch.Stop();

                actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>();
                actualException!.Message.Should().Be("The ReadAsync operation was cancelled.");

                stopWatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
            }
        }

        [Test]
        [StreamMethodTestCase]
        public async Task UsingAStreamThatHasAlreadyTimedOutShouldRethrowTheTimeoutException(StreamMethod streamMethod)
        {
            var (disposables, sut, callCountingStream, _, _) = await BuildTcpClientAndTcpListener(CancellationToken);

            using (disposables)
            {
                sut.WriteTimeout = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;
                sut.ReadTimeout = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;
                
                var exception = await Try.CatchingError(async () => await sut.ReadFromStream(streamMethod, new byte[19], 0, 19, CancellationToken));
                
                callCountingStream.Reset();

                exception.Should().NotBeNull();
                AssertExceptionsAreEqual(exception!, AssertionExtensions.Should(() => sut.Position).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.Position = 0).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.CanRead).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.CanSeek).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.CanTimeout).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.CanWrite).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.Length).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.ReadTimeout).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.ReadTimeout = 1).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.WriteTimeout).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.WriteTimeout = 1).Throw<Exception>().And);

                var memoryStream = new MemoryStream();
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.ReadByte()).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.Flush()).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.Read(new byte[1], 0, 1)).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.Seek(0, SeekOrigin.Begin)).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.SetLength(0)).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.Write(new byte[1], 0, 1)).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.WriteByte(0)).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.CopyTo(memoryStream)).Throw<Exception>().And);

#if !NETFRAMEWORK
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.Read(new Span<byte>(new byte[1]))).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.Write(new ReadOnlySpan<byte>(new byte[1]))).Throw<Exception>().And);
#endif

#if NETFRAMEWORK
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.CreateObjRef(typeof(NetworkTimeoutStream))).Throw<Exception>().And);
                AssertExceptionsAreEqual(exception, AssertionExtensions.Should(() => sut.InitializeLifetimeService()).Throw<Exception>().And);
#endif
                
                AssertExceptionsAreEqual(exception, (await AssertionExtensions.Should(() => sut.CopyToAsync(memoryStream, 0, CancellationToken.None)).ThrowAsync<Exception>()).And);
                AssertExceptionsAreEqual(exception, (await AssertionExtensions.Should(() => sut.FlushAsync()).ThrowAsync<Exception>()).And);
                AssertExceptionsAreEqual(exception, (await AssertionExtensions.Should(() => sut.ReadAsync(new byte[1], 0, 1, CancellationToken.None)).ThrowAsync<Exception>()).And);
                AssertExceptionsAreEqual(exception, (await AssertionExtensions.Should(() => sut.WriteAsync(new byte[1], 0, 1, CancellationToken.None)).ThrowAsync<Exception>()).And);
                
#if !NETFRAMEWORK
                AssertExceptionsAreEqual(exception, (await AssertionExtensions.Should(() => sut.ReadAsync(new Memory<byte>(new byte[1]), CancellationToken.None).AsTask()).ThrowAsync<Exception>()).And);
                AssertExceptionsAreEqual(exception, (await AssertionExtensions.Should(() => sut.WriteAsync(new ReadOnlyMemory<byte>(new byte[1]), CancellationToken.None).AsTask()).ThrowAsync<Exception>()).And);
#endif
                callCountingStream.FlushAsyncCallCount.Should().Be(0);
                callCountingStream.ReadAsyncCallCount.Should().Be(0);
                callCountingStream.WriteAsyncCallCount.Should().Be(0);
                callCountingStream.ReadMemoryAsyncCallCount.Should().Be(0);
                callCountingStream.WriteMemoryAsyncCallCount.Should().Be(0);
                callCountingStream.CopyToAsyncCallCount.Should().Be(0);
                callCountingStream.ReadByteCallCount.Should().Be(0);
                callCountingStream.BeginReadCallCount.Should().Be(0);
                callCountingStream.EndReadCallCount.Should().Be(0);
                callCountingStream.BeginWriteCallCount.Should().Be(0);
                callCountingStream.EndWriteCallCount.Should().Be(0);
                callCountingStream.FlushCallCount.Should().Be(0);
                callCountingStream.ReadCallCount.Should().Be(0);
                callCountingStream.SeekCallCount.Should().Be(0);
                callCountingStream.SetLengthCallCount.Should().Be(0);
                callCountingStream.WriteCallCount.Should().Be(0);
                callCountingStream.WriteByteCallCount.Should().Be(0);
                callCountingStream.CopyToCallCount.Should().Be(0);
                callCountingStream.ReadSpanCallCount.Should().Be(0);
                callCountingStream.WriteSpanCallCount.Should().Be(0);
                callCountingStream.CreateObjRefCallCount.Should().Be(0);
                callCountingStream.InitializeLifetimeServiceCallCount.Should().Be(0);

                // Close and Dispose should not re-throw on timeout as callers do not expect these to throw 
                // e.g. if using ... throw it makes the stream difficult to use
                sut.Close();
                sut.Dispose();
                await sut.DisposeAsync();

                callCountingStream.CloseCallCount.Should().Be(2);
                callCountingStream.DisposeBoolCallCount.Should().Be(0);
                callCountingStream.DisposeAsyncCallCount.Should().Be(1);
            }
        }

        static void AssertStreamWasClosed(SyncOrAsync syncOrAsync, CallCountingStream callCountingStream)
        {
            if (syncOrAsync == SyncOrAsync.Sync)
            {
                callCountingStream.CloseCallCount.Should().Be(1, "The Stream should have been Closed on Timeout");
            }
            else
            {
                callCountingStream.DisposeAsyncCallCount.Should().Be(1, "The Stream should have been DisposedAsync on Timeout");
            }
        }

        static void AssertStreamWasClosed(StreamMethod streamMethod, CallCountingStream callCountingStream)
        {
            AssertStreamWasClosed(streamMethod == StreamMethod.Sync ? SyncOrAsync.Sync : SyncOrAsync.Async, callCountingStream);
        }

        static void AssertStreamWasClosed(StreamReadMethod streamReadMethod, CallCountingStream callCountingStream)
        {
            switch (streamReadMethod)
            {
                case StreamReadMethod.ReadAsync: 
#if !NETFRAMEWORK
                case StreamReadMethod.ReadAsyncForMemoryByteArray:
#endif
                case StreamReadMethod.BeginReadEndOutsideCallback: 
                case StreamReadMethod.BeginReadEndWithinCallback: 
                    AssertStreamWasClosed(SyncOrAsync.Async, callCountingStream);
                    break;
                default:
                    AssertStreamWasClosed(SyncOrAsync.Sync, callCountingStream);
                    break;
            }
        }

        static void AssertStreamWasClosed(StreamWriteMethod streamWriteMethod, CallCountingStream callCountingStream)
        {
            switch (streamWriteMethod)
            {
                case StreamWriteMethod.WriteAsync: 
#if !NETFRAMEWORK
                case StreamWriteMethod.WriteAsyncForMemoryByteArray:
#endif
                case StreamWriteMethod.BeginWriteEndOutsideCallback: 
                case StreamWriteMethod.BeginWriteEndWithinCallback: 
                    AssertStreamWasClosed(SyncOrAsync.Async, callCountingStream);
                    break;
                default:
                    AssertStreamWasClosed(SyncOrAsync.Sync, callCountingStream);
                    break;
            }
        }

        static void AssertStreamWasClosed(StreamCopyToMethod streamCopyToMethod, CallCountingStream callCountingStream)
        {
            switch (streamCopyToMethod)
            {
                case StreamCopyToMethod.CopyToAsync:
                case StreamCopyToMethod.CopyToAsyncWithBufferSize:
                    AssertStreamWasClosed(SyncOrAsync.Async, callCountingStream);
                    break;
                default:
                    AssertStreamWasClosed(SyncOrAsync.Sync, callCountingStream);
                    break;
            }
        }

        void AssertExceptionsAreEqual(Exception expected, Exception actual)
        {
            expected.GetType().Should().Be(actual.GetType());
            expected.Message.Should().Be(actual.Message);
        }

        static async Task DelayForeverToTryAndDelayWriting(CancellationToken cancellationToken)
        {
            await Task.Delay(-1, cancellationToken);
        }

        async Task<(IDisposable Disposables, NetworkTimeoutStream SystemUnderTest, CallCountingStream callCountingStream, PausingStream pausingStream, Func<string, Task> PerformServiceWriteFunc)> BuildTcpClientAndTcpListener(
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
            var pausableStream = new PausingStream(clientStream);
            var callCountingStream = new CallCountingStream(pausableStream);
            var sut = new NetworkTimeoutStream(callCountingStream);

            disposableCollection.AddIgnoringDisposalError(sut);

            while (performServiceWriteFunc == null)
            { 
                await Task.Delay(10, cancellationToken);
            }

            return (disposableCollection, sut, callCountingStream, pausableStream, performServiceWriteFunc!);
        }
    }
}
