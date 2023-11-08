using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;

namespace Halibut
{
    public class DataStream : IEquatable<DataStream>, IDataStreamInternal
    {
        readonly Func<Stream, CancellationToken, Task> writerAsync;
        IDataStreamReceiver receiver;

        [JsonConstructor]
        public DataStream()
        {
        }
        
        public DataStream(long length, Func<Stream, CancellationToken, Task> writerAsync)
        {
            Length = length;
            Id = Guid.NewGuid();
            this.writerAsync = writerAsync;
        }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("length")]
        public long Length { get; set; }

        public IDataStreamReceiver Receiver()
        {
            if (receiver != null)
            {
                return receiver;
            }

            // Use a FileStream for packages over 2GB, or you risk running into OutOfMemory
            // exceptions with MemoryStream.
            var maxMemoryStreamLength = int.MaxValue;
            if (Length >= maxMemoryStreamLength)
            {
                return new TemporaryFileDataStreamReceiver(writerAsync);
            }
            
            return new InMemoryDataStreamReceiver(writerAsync);
        }

        public bool Equals(DataStream other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DataStream)obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(DataStream left, DataStream right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(DataStream left, DataStream right)
        {
            return !Equals(left, right);
        }

        public static DataStream FromBytes(byte[] data)
        {
            return new DataStream(data.Length,
                async (stream, ct) =>
                    {
                        await stream.WriteAsync(data, 0, data.Length, ct);
                    });
        }

        public static DataStream FromString(string text)
        {
            return FromString(text, new UTF8Encoding(false));
        }

        public static DataStream FromString(string text, Encoding encoding)
        {
            return new DataStream(encoding.GetByteCount(text), 
                async (stream, ct) =>
                {
                    var writer = new StreamWriter(stream, encoding);
                    // TODO - ASYNC ME UP!
                    // Writer does not support taking a cancellation token, should we do something else here?
                    await writer.WriteAsync(text);
                    await writer.FlushAsync();
                });
        }
        
        public static DataStream FromStream(Stream source, Func<int, CancellationToken, Task> updateProgressAsync)
        {
            var streamer = new StreamingDataStream(source, updateProgressAsync);

            return new DataStream(source.Length, streamer.CopyAndReportProgressAsync);
        }
        
        public static DataStream FromStream(Stream source)
        {
            return FromStream(source, async (_, _) => { await Task.CompletedTask;});
        }

        class StreamingDataStream
        {
            const int BufferSize = 84000;
            readonly Stream source;
            readonly Func<int, CancellationToken, Task> updateProgressAsync;

            public StreamingDataStream(Stream source, Func<int, CancellationToken, Task> updateProgressAsync)
            {
                this.source = source;
                this.updateProgressAsync = updateProgressAsync;
            }
            
            public async Task CopyAndReportProgressAsync(Stream destination, CancellationToken cancellationToken)
            {
                var readBuffer = new byte[BufferSize];
                var writeBuffer = new byte[BufferSize];

                var progress = 0;
                
                var totalLength = source.Length;
                long copiedSoFar = 0;
                source.Seek(0, SeekOrigin.Begin);

                var count = await source.ReadAsync(readBuffer, 0, BufferSize, cancellationToken);
                while (count > 0)
                {
                    Swap(ref readBuffer, ref writeBuffer);
                    var writeTask = destination.WriteAsync(writeBuffer, 0, count, cancellationToken);
                    count = await source.ReadAsync(readBuffer, 0, BufferSize, cancellationToken);
                    await writeTask;

                    copiedSoFar += count;

                    var progressNow = (int)((double)copiedSoFar / totalLength * 100.00);
                    if (progressNow == progress)
                        continue;
                    await updateProgressAsync(progressNow, cancellationToken);
                    progress = progressNow;
                }

                if (progress != 100)
                    await updateProgressAsync(100, cancellationToken);

                await destination.FlushAsync(cancellationToken);
            }

            static void Swap<T>(ref T x, ref T y)
            {
                T tmp = x;
                x = y;
                y = tmp;
            }
        }

        async Task IDataStreamInternal.TransmitAsync(Stream stream, CancellationToken cancellationToken)
        {
            await writerAsync(stream, cancellationToken);
        }

        void IDataStreamInternal.Received(IDataStreamReceiver attachedReceiver)
        {
            receiver = attachedReceiver;
        }
    }
}