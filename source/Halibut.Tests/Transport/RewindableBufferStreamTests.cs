using System;
using System.IO;
using System.Text;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class RewindableBufferStreamTests
    {
        [Test]
        public void Read_ShouldPassThrough()
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
        public void Read_ZeroRewindShouldPassThrough()
        {
            using (var baseStream = new MemoryStream(16))
            using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartRewindBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                Assert.AreEqual(4, sut.Read(outputBuffer, 0, inputBuffer.Length));
                var initialReadValue = Encoding.ASCII.GetString(outputBuffer);
                Assert.AreEqual(initialReadValue, "Test");
                sut.FinishRewindBuffer(0);

                var rewoundOutputBuffer = new byte[4];
                Assert.AreEqual(0, sut.Read(rewoundOutputBuffer, 0, 4));
            }
        }

        [Test]
        public void Read_ShouldReadRewindBufferAfterRewind()
        {
            using (var baseStream = new MemoryStream(16))
            using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);
                baseStream.Position = 0;

                sut.StartRewindBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                _ = sut.Read(outputBuffer, 0, inputBuffer.Length);
                sut.FinishRewindBuffer(2);

                var rewoundOutputBuffer = new byte[8];
                Assert.AreEqual(2, sut.Read(rewoundOutputBuffer, 0, 8));
                var postRewindValue = Encoding.ASCII.GetString(rewoundOutputBuffer.AsSpan(0, 2).ToArray());
                Assert.AreEqual("st", postRewindValue);
            }
        }

        [Test]
        public void Cancel_ShouldNotRewind()
        {
            using (var baseStream = new MemoryStream(16))
            using (var sut = RewindableBufferStreamBuilder.Build(baseStream))
            {
                var inputBuffer = Encoding.ASCII.GetBytes("Test");
                baseStream.Write(inputBuffer, 0, inputBuffer.Length);

                baseStream.Position = 0;

                sut.StartRewindBuffer();
                var outputBuffer = new byte[inputBuffer.Length];
                _ = sut.Read(outputBuffer, 0, inputBuffer.Length);
                sut.CancelRewindBuffer();

                var rewoundOutputBuffer = new byte[1];
                Assert.AreEqual(0, sut.Read(rewoundOutputBuffer, 0, 1));
            }
        }

        static class RewindableBufferStreamBuilder
        {
            public static RewindableBufferStream Build(Stream stream) => new RewindableBufferStream(stream);
        }
    }
}
