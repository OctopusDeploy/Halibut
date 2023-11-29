using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Transport.Streams;
using Halibut.Transport;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class RewindableBufferStreamFixture
    {
        [Test]
        public void ReadShouldPassThrough()
        {
            using (var baseStream = new MemoryStream(16))
            using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);

                baseStream.Position = 0;

                using (var streamReader = new StreamReader(sut))
                {
                    Assert.AreEqual("Test", streamReader.ReadToEnd());
                }
            }
        }

        [Test]
        public async Task ReadAsyncShouldPassThrough()
        {
            using (var baseStream = new MemoryStream(16))
            await using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                await baseStream.WriteAsync(inputBuffer, 0, inputBuffer.Length);

                baseStream.Position = 0;

                using (var streamReader = new StreamReader(sut))
                {
                    Assert.AreEqual("Test", await streamReader.ReadToEndAsync());
                }
            }
        }
        
        [Test]
        public async Task ReadAsyncShouldPassTheCancellationTokenToTheUnderlyingStream()
        {
            using (var baseStream = new MemoryStream(16))
            await using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                await baseStream.WriteAsync(inputBuffer, 0, inputBuffer.Length);

                baseStream.Position = 0;

                var readBuffer = new byte[inputBuffer.Length];
                var cts = new CancellationTokenSource();
                cts.Cancel();
                
                Func<Task<int>> readAsyncCall = async () => await sut.ReadAsync(readBuffer, 0, readBuffer.Length, cts.Token);

                await readAsyncCall.Should().ThrowAsync<TaskCanceledException>();
            }
        }

        [Test]
        public void ReadZeroRewindShouldPassThrough()
        {
            using (var baseStream = new MemoryStream(16))
            using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                Assert.AreEqual(4, sut.Read(outputBuffer, 0, inputBuffer.Length));
                var initialReadValue = Encoding.ASCII.GetString(outputBuffer);
                Assert.AreEqual(initialReadValue, "Test");
                sut.FinishAndRewind(0);

                var rewoundOutputBuffer = new byte[4];
                Assert.AreEqual(0, sut.Read(rewoundOutputBuffer, 0, 4));
            }
        }

        [Test]
        public async Task ReadAsyncZeroRewindShouldPassThrough()
        {
            using (var baseStream = new MemoryStream(16))
            await using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                await baseStream.WriteAsync(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                Assert.AreEqual(4, await sut.ReadAsync(outputBuffer, 0, inputBuffer.Length));
                var initialReadValue = Encoding.ASCII.GetString(outputBuffer);
                Assert.AreEqual(initialReadValue, "Test");
                sut.FinishAndRewind(0);

                var rewoundOutputBuffer = new byte[4];
                Assert.AreEqual(0, await sut.ReadAsync(rewoundOutputBuffer, 0, 4));
            }
        }

        [Test]
        public void ReadShouldReadRewindBufferAfterRewind()
        {
            using (var baseStream = new MemoryStream(16))
            using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                _ = sut.Read(outputBuffer, 0, inputBuffer.Length);
                sut.FinishAndRewind(2);

                var rewoundOutputBuffer = new byte[8];
                Assert.AreEqual(2, sut.Read(rewoundOutputBuffer, 0, 8));
                var postRewindValue = Encoding.ASCII.GetString(rewoundOutputBuffer.AsSpan(0, 2).ToArray());
                Assert.AreEqual("st", postRewindValue);
            }
        }

        [Test]
        public void ReadShouldReadRewindBufferAfterRewindAndHasStartedBufferingAgain()
        {
            using (var baseStream = new MemoryStream(16))
            using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                _ = sut.Read(outputBuffer, 0, inputBuffer.Length);
                sut.FinishAndRewind(2);

                sut.StartBuffer();
                var rewoundOutputBuffer = new byte[8];
                Assert.AreEqual(2, sut.Read(rewoundOutputBuffer, 0, 8));
                var postRewindValue = Encoding.ASCII.GetString(rewoundOutputBuffer.AsSpan(0, 2).ToArray());
                Assert.AreEqual("st", postRewindValue);
            }
        }

        [Test]
        public async Task ReadAsyncShouldReadRewindBufferAfterRewind()
        {
            using (var baseStream = new MemoryStream(16))
            await using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                await baseStream.WriteAsync(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                _ = await sut.ReadAsync(outputBuffer, 0, inputBuffer.Length);
                sut.FinishAndRewind(2);

                var rewoundOutputBuffer = new byte[8];
                Assert.AreEqual(2, await sut.ReadAsync(rewoundOutputBuffer, 0, 8));
                var postRewindValue = Encoding.ASCII.GetString(rewoundOutputBuffer.AsSpan(0, 2).ToArray());
                Assert.AreEqual("st", postRewindValue);
            }
        }

        [Test]
        public async Task ReadAsyncShouldReadRewindBufferAfterRewindAndHasStartedBufferingAgain()
        {
            using (var baseStream = new MemoryStream(16))
            await using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                await baseStream.WriteAsync(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                _ = await sut.ReadAsync(outputBuffer, 0, inputBuffer.Length);
                sut.FinishAndRewind(2);

                sut.StartBuffer();
                var rewoundOutputBuffer = new byte[8];
                Assert.AreEqual(2, await sut.ReadAsync(rewoundOutputBuffer, 0, 8));
                var postRewindValue = Encoding.ASCII.GetString(rewoundOutputBuffer.AsSpan(0, 2).ToArray());
                Assert.AreEqual("st", postRewindValue);
            }
        }

        [Test]
        public async Task CancelShouldNotRewind()
        {
            using (var baseStream = new MemoryStream(16))
            await using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);

                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                await sut.ReadAsync(outputBuffer, 0, inputBuffer.Length);
                sut.CancelBuffer();

                var rewoundOutputBuffer = new byte[1];
                Assert.AreEqual(0, await sut.ReadAsync(rewoundOutputBuffer, 0, 1));
            }
        }
        
        [Test]
        public void ReadingMoreThanTheBufferSizeShouldSupportRewinding()
        {
            using (var baseStream = new MemoryStream(2))
            using (var sut =  new RewindableBufferStream(baseStream, 2))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                while (sut.Read(outputBuffer, 0, inputBuffer.Length) > 0)
                {
                }
                
                sut.FinishAndRewind(2);

                var rewoundOutputBuffer = new byte[8];
                Assert.AreEqual(2, sut.Read(rewoundOutputBuffer, 0, 8));
                var postRewindValue = Encoding.ASCII.GetString(rewoundOutputBuffer.AsSpan(0, 2).ToArray());
                Assert.AreEqual("st", postRewindValue);
            }
        }
        
        [Test]
        public async Task ReadingMoreThanTheBufferSizeShouldSupportRewindingAsync()
        {
            using (var baseStream = new MemoryStream(2))
            await using (var sut =  new RewindableBufferStream(baseStream, 2))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                await baseStream.WriteAsync(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                while (await sut.ReadAsync(outputBuffer, 0, inputBuffer.Length) > 0)
                {
                }
                sut.FinishAndRewind(2);

                var rewoundOutputBuffer = new byte[8];
                Assert.AreEqual(2, await sut.ReadAsync(rewoundOutputBuffer, 0, 8));
                var postRewindValue = Encoding.ASCII.GetString(rewoundOutputBuffer.AsSpan(0, 2).ToArray());
                Assert.AreEqual("st", postRewindValue);
            }
        }

        static class RewindableBufferStreamBuilder
        {
            public static RewindableBufferStream Build(Stream stream) => new RewindableBufferStream(stream);
        }
    }

    public class RewindableBufferStreamIsAsyncFixture : StreamWrapperSupportsAsyncIOFixture
    {
        protected override AsyncStream WrapStream(Stream stream) => new RewindableBufferStream(stream);
    }
}
