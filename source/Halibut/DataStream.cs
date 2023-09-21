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
        readonly Action<Stream> writer;
        readonly Func<Stream, CancellationToken, Task> writerAsync;
        IDataStreamReceiver receiver;

        [JsonConstructor]
        public DataStream()
        {
        }
        
        public DataStream(long length, Action<Stream> writer, Func<Stream, CancellationToken, Task> writerAsync)
        {
            Length = length;
            Id = Guid.NewGuid();
            this.writer = writer;
            this.writerAsync = writerAsync;
        }

        [Obsolete]
        public DataStream(long length, Action<Stream> writer)
            : this(
                length,
                writer,
                async (stream, ct) =>
                {
                    await Task.CompletedTask;
                    writer(stream);
                })
        {
        }

        public DataStream(long length, Func<Stream, CancellationToken, Task> writerAsync)
            : this(length, stream => writerAsync(stream, CancellationToken.None).GetAwaiter().GetResult(), writerAsync)
        {
        }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("length")]
        public long Length { get; set; }

        public IDataStreamReceiver Receiver()
        {
            if (receiver != null)
                return receiver;

            // Use a FileStream for packages over 2GB, or you risk running into OutOfMemory
            // exceptions with MemoryStream.
            var maxMemoryStreamLength = int.MaxValue;
            if (Length >= maxMemoryStreamLength)
                return new TemporaryFileDataStreamReceiver(writer, writerAsync);
            else
                return new InMemoryDataStreamReceiver(writer, writerAsync);
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
                stream => stream.Write(data, 0, data.Length), 
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
                stream =>
                {
                    var writer = new StreamWriter(stream, encoding);
                    writer.Write(text);
                    writer.Flush();
                },
                async (stream, ct) =>
                {
                    var writer = new StreamWriter(stream, encoding);
                    // TODO - ASYNC ME UP!
                    // Writer does not support taking a cancellation token, should we do something else here?
                    await writer.WriteAsync(text);
                    await writer.FlushAsync();
                });
        }
        
        public static DataStream FromStreamAsync(Stream source, Func<int, CancellationToken, Task> updateProgress)
        {
            updateProgress ??= (_, _) => Task.CompletedTask;

#pragma warning disable CS0612
            return FromStream(source, 
                i => updateProgress(i, CancellationToken.None).GetAwaiter().GetResult(),
                updateProgress);
#pragma warning restore CS0612
        }

        [Obsolete]
        public static DataStream FromStream(Stream source, Action<int> updateProgress)
        {
            updateProgress ??= _ => { };

            return FromStream(source, updateProgress, (i, token) =>
            {
                updateProgress(i);
                return Task.CompletedTask;
            });
        }
        
        public static DataStream FromStream(Stream source, Action<int> updateProgress, Func<int, CancellationToken, Task> updateProgressAsync)
        {
            var streamer = new StreamingDataStream(source, updateProgress, updateProgressAsync);
#pragma warning disable CS0612
            return new DataStream(source.Length, streamer.CopyAndReportProgress, streamer.CopyAndReportProgressAsync);
#pragma warning restore CS0612
        }
        
        public static DataStream FromStream(Stream source)
        {
#pragma warning disable CS0612
            return FromStream(source, (progress) => { });
#pragma warning restore CS0612
        }

        class StreamingDataStream
        {
            const int BufferSize = 84000;
            readonly Stream source;
            readonly Action<int> updateProgress;
            readonly Func<int, CancellationToken, Task> updateProgressAsync;

            public StreamingDataStream(Stream source, Action<int> updateProgress, Func<int, CancellationToken, Task> updateProgressAsync)
            {
                this.source = source;
                this.updateProgress = updateProgress;
                this.updateProgressAsync = updateProgressAsync;
            }

            [Obsolete]
            public void CopyAndReportProgress(Stream destination)
            {
                var readBuffer = new byte[BufferSize];
                var writeBuffer = new byte[BufferSize];

                var progress = 0;
                
                var totalLength = source.Length;
                long copiedSoFar = 0;
                source.Seek(0, SeekOrigin.Begin);

                var count = source.Read(readBuffer, 0, BufferSize);
                while (count > 0)
                {
                    Swap(ref readBuffer, ref writeBuffer);
                    var asyncResult = destination.BeginWrite(writeBuffer, 0, count, null, null);
                    count = source.Read(readBuffer, 0, BufferSize);
                    destination.EndWrite(asyncResult);

                    copiedSoFar += count;

                    var progressNow = (int)((double)copiedSoFar / totalLength * 100.00);
                    if (progressNow == progress)
                        continue;
                    updateProgress(progressNow);
                    progress = progressNow;
                }

                if (progress != 100)
                    updateProgress(100);

                destination.Flush();
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

        [Obsolete]
        void IDataStreamInternal.Transmit(Stream stream)
        {
            writer(stream);
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