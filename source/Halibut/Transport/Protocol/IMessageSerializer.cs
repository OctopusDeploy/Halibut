using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Streams;

namespace Halibut.Transport.Protocol
{
    public interface IMessageSerializer
    {
        Task<IReadOnlyList<DataStream>> WriteMessageAsync<T>(Stream stream, T message, CancellationToken cancellationToken);
        Task<(T Message, IReadOnlyList<DataStream> DataStreams, byte[]? CompressedMessageBytes)> ReadMessageAsync<T>(
            RewindableBufferStream stream, bool captureData, CancellationToken cancellationToken);
    }
}