using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;
using Halibut.Transport;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    public class StreamExtensionMethodsFixture : BaseTest
    {
        public class FunForTheNetworkLongs : IEnumerable<long>
        {
            public IEnumerator<long> GetEnumerator()
            {
                yield return 0;
                yield return 1;
                yield return int.MaxValue;
                yield return int.MinValue;
                
                yield return int.MaxValue + 1L;
                yield return int.MinValue - 1L;
                
                yield return long.MaxValue;
                yield return long.MinValue;
                yield return 44093703243L; // 101001000100001100001000010001001011 It looks sort of interesting, maybe we will mess it up when we transmit it.
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>
        /// The BinaryWriter is what Halibut used when code was sync, so we need to match that.
        /// </summary>
        /// <param name="l"></param>
        [Test]
        public async Task WriteLongAsyncMatchesBinaryWriter([ValuesOfType(typeof(FunForTheNetworkLongs))] long l)
        {
            using var memoryStream = new MemoryStream();
            await memoryStream.WriteLongAsync(l, CancellationToken);
            var result = memoryStream.ToArray();
            result.Should().BeEquivalentTo(BytesFromBinaryWriter(l));
        }
        
        [Test]
        public async Task WriteLongAsyncWorksWithReadInt64Async([ValuesOfType(typeof(FunForTheNetworkLongs))] long l)
        {
            using var memoryStream = new MemoryStream();
            await memoryStream.WriteLongAsync(l, CancellationToken);
            memoryStream.WriteString("Random stuff");
            memoryStream.Position = 0;
            var res = await memoryStream.ReadInt64Async(CancellationToken);
            res.Should().Be(l);
        }
        
        /// <summary>
        /// The BinaryWriter is what Halibut used when code was sync, so we need to match that.
        /// </summary>
        /// <param name="l"></param>
        [Test]
        public async Task ReadInt64AsyncWorkWithBinaryWriter([ValuesOfType(typeof(FunForTheNetworkLongs))] long l)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(l);
            binaryWriter.Flush();
            memoryStream.WriteString("Random stuff");
            memoryStream.Position = 0;
            var res = await memoryStream.ReadInt64Async(CancellationToken);
            res.Should().Be(l);
        }

        [Test]
        public async Task ReadBytesAsyncWillKeepReadingUntilItHasReadEnoughBytes(
            [Values(1,2,3, 7, 21)] int readSize)
        {
            using var memoryStream = new MemoryStream();
            memoryStream.WriteString("ReadBytesAsyncWillReadUntilItHasReadEnoughBytes");
            memoryStream.Position = 0;

            var limitedReadingStream = new LimitedNumberOfBytesPerReadStream(memoryStream, readSize);

            var bytes = await limitedReadingStream.ReadBytesAsync(29, CancellationToken);

            Encoding.UTF8.GetString(bytes).Should().Be("ReadBytesAsyncWillReadUntilIt");
        }
        
        [Test]
        public async Task ReadBytesAsyncWillStopReadingIfTheEndIsReached()
        {
            using var memoryStream = new MemoryStream();
            memoryStream.WriteString("not enough");
            memoryStream.Position = 0;
            
            await AssertAsync.Throws<EndOfStreamException>(async () => await memoryStream.ReadBytesAsync(1000, CancellationToken));
        }

        byte[] BytesFromBinaryWriter(long l)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(l);
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
    }
}