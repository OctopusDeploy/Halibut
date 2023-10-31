using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class DeflateStreamInputBufferReflectorFixture
    {
        [Test]
        public async Task TryGetAvailableInputBufferSizeShouldReadSize()
        {
            using var stream = new MemoryStream();

            // Write both compressed and uncompressed data to the stream.
            using (var inputDeflate = new DeflateStream(stream, CompressionMode.Compress, true))
            {
                var bytes = Encoding.ASCII.GetBytes("Compressed");
                await inputDeflate.WriteByteArrayAsync(bytes, CancellationToken.None); // length = 10
            }

            var notCompressedBytes = Encoding.ASCII.GetBytes("Not Compressed");
            await stream.WriteByteArrayAsync(notCompressedBytes, CancellationToken.None); // length = 14
            stream.Position = 0;

            // Now decompress, filling the DeflateStream buffer
            using var deflate = new DeflateStream(stream, CompressionMode.Decompress);
            _ = await deflate.ReadAsync(new byte[10], 0, 10, CancellationToken.None);

            var sut = new DeflateStreamInputBufferReflector(new InMemoryConnectionLog("poll://foo/"));
            Assert.IsTrue(sut.TryGetAvailableInputBufferSize(deflate, out var result));
            Assert.AreEqual(14, result);
        }
    }
}
