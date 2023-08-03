using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Halibut.Diagnostics;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class DeflateStreamInputBufferReflectorFixture
    {
        [Test]
        public void TryGetAvailableInputBufferSizeShouldReadSize()
        {
            using var stream = new MemoryStream();

            // Write both compressed and uncompressed data to the stream.
            using (var inputDeflate = new DeflateStream(stream, CompressionMode.Compress, true))
            {
                var bytes = Encoding.ASCII.GetBytes("Compressed");
                inputDeflate.Write(bytes, 0, bytes.Length); // length = 10
            }
            var notCompressedBytes = Encoding.ASCII.GetBytes("Not Compressed");
            stream.Write(notCompressedBytes, 0, notCompressedBytes.Length); // length = 14
            stream.Position = 0;

            // Now decompress, filling the DeflateStream buffer
            using var deflate = new DeflateStream(stream, CompressionMode.Decompress);
            _ = deflate.Read(new byte[10], 0, 10);

            var sut = new DeflateStreamInputBufferReflector(new InMemoryConnectionLog("poll://foo/"));
            Assert.IsTrue(sut.TryGetAvailableInputBufferSize(deflate, out var result));
            Assert.AreEqual(14, result);
        }
    }
}
