using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.DataStreams;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;

namespace Halibut
{
    public class DataStream : IEquatable<DataStream>, IDataStreamInternal
    {
        protected Func<Stream, CancellationToken, Task> writerAsync;
        IDataStreamReceiver? receiver;

        [JsonConstructor]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public DataStream()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
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

        public bool Equals(DataStream? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id.Equals(other.Id);
        }

        public override bool Equals(object? obj)
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
        
            return new DataStreamWithFileUploadProgress(source, new PercentageCompleteDataStreamTransferProgress(updateProgressAsync, source.Length));
        }
        
        public static DataStream FromStream(Stream source)
        {
            return new DataStream(source.Length, new StreamCopierWithProgress(source, new NoOpDataStreamTransferProgress()).CopyAndReportProgressAsync);
        }

        async Task IDataStreamInternal.TransmitAsync(Stream stream, CancellationToken cancellationToken)
        {
            await writerAsync(stream, cancellationToken);
        }

        public async Task WriteData(Stream stream, CancellationToken cancellationToken)
        {
            await writerAsync(stream, cancellationToken);
        }

        void IDataStreamInternal.Received(IDataStreamReceiver attachedReceiver)
        {
            receiver = attachedReceiver;
        }

        /// <summary>
        /// Used to re-hydrate deserialised data streams, which won't have a writer set. 
        /// </summary>
        /// <param name="writerAsync"></param>
        public void SetWriterAsync(Func<Stream, CancellationToken, Task> writerAsync)
        {
            if(this.writerAsync != null) throw new InvalidOperationException("Cannot set writer more than once.");
            this.writerAsync = writerAsync;
        }
    }
}