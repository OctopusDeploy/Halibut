using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Streams;

namespace Halibut.Tests.Support.Streams
{
//     public class PauseableStream : AsyncStream
//     {
//         Stream innerStream;
//
//         public PauseableStream(Stream innerStream)
//         {
//             this.innerStream = innerStream;
//         }
//
//         public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//         {
//             return await innerStream.ReadAsync(buffer, offset, count, cancellationToken);
//         }
//
//
//         public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//         {
//             await innerStream.WriteAsync(buffer, offset, count, cancellationToken);
//         }
//
//         public override Task FlushAsync(CancellationToken cancellationToken)
//         {
//             await innerStream.Wr
//         };
//
// #if !NETFRAMEWORK
//         public override ValueTask DisposeAsync()
//         {
//             
//         }
// #endif
//         
//     }
}