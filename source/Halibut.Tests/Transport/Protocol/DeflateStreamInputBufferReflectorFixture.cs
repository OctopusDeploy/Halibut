using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class DeflateStreamInputBufferReflectorFixture
    {
        [Test]
        [SyncAndAsync]
        public async Task TryGetAvailableInputBufferSizeShouldReadSize(SyncOrAsync syncOrAsync)
        {
            using var stream = new MemoryStream();

            // Write both compressed and uncompressed data to the stream.
            using (var inputDeflate = new DeflateStream(stream, CompressionMode.Compress, true))
            {
                var bytes = Encoding.ASCII.GetBytes("Compressed");
                await syncOrAsync.WriteToStream(inputDeflate, bytes); // length = 10
            }
            var notCompressedBytes = Encoding.ASCII.GetBytes("Not Compressed");
            await syncOrAsync.WriteToStream(stream, notCompressedBytes); // length = 14
            stream.Position = 0;

            // Now decompress, filling the DeflateStream buffer
            using var deflate = new DeflateStream(stream, CompressionMode.Decompress);
            _ = await syncOrAsync.ReadFromStream(deflate, new byte[10], 0, 10);

            var sut = new DeflateStreamInputBufferReflector(new InMemoryConnectionLog("poll://foo/"));
            Assert.IsTrue(sut.TryGetAvailableInputBufferSize(deflate, out var result));
            Assert.AreEqual(14, result);
        }
    }
}
