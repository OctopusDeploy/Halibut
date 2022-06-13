using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class DeflateStreamInputBufferReflectorTests
    {
        #if NETFRAMEWORK
        [Test]
        public void TryGetAvailableInputBufferSizeShouldThrow()
        {
            var sut = new DeflateStreamInputBufferReflector();

            using (var ms = new MemoryStream())
            using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
            {
                Assert.Throws<PlatformNotSupportedException>(() => sut.TryGetAvailableInputBufferSize(deflate, out _));
            }
        }
        #else
        [Test]
        public void TryGetAvailableInputBufferSizeShouldReadSize()
        {
            using var stream = new MemoryStream();

            // Write both compressed and uncompressed data to the stream.
            using (var inputDeflate = new DeflateStream(stream, CompressionMode.Compress, true))
            {
                inputDeflate.Write(Encoding.ASCII.GetBytes("Compressed")); // length = 10
            }
            stream.Write(Encoding.ASCII.GetBytes("Not Compressed")); // length = 14
            stream.Position = 0;

            // Now decompress, filling the DeflateStream buffer
            using var deflate = new DeflateStream(stream, CompressionMode.Decompress);
            _ = deflate.Read(new byte[10], 0, 10);

            var sut = new DeflateStreamInputBufferReflector();
            Assert.IsTrue(sut.TryGetAvailableInputBufferSize(deflate, out var result));
            Assert.AreEqual(14, result);
        }
        #endif
    }
}
