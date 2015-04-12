using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Halibut
{
    public class DataStream : IEquatable<DataStream>
    {
        readonly Action<Stream> writer;
        Action<Action<Stream>> attachedReader;

        [JsonConstructor]
        public DataStream()
        {
        }

        public DataStream(long length, Action<Stream> writer)
        {
            Length = length;
            Id = Guid.NewGuid();
            this.writer = writer;
        }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("length")]
        public long Length { get; set; }

        public void Attach(Action<Action<Stream>> reader)
        {
            attachedReader = reader;
        }

        public void Read(Action<Stream> reader)
        {
            attachedReader(reader);
        }

        public void Write(Stream stream)
        {
            writer(stream);
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
            return new DataStream(data.Length, stream => stream.Write(data, 0, data.Length));
        }

        public static DataStream FromString(string text)
        {
            return FromString(text, new UTF8Encoding(false));
        }

        public static DataStream FromString(string text, Encoding encoding)
        {
            return new DataStream(encoding.GetByteCount(text), stream =>
            {
                var writer = new StreamWriter(stream, encoding);
                writer.Write(text);
                writer.Flush();
            });
        }

        public static DataStream FromStream(Stream source, Action<int> updateProgress)
        {
            var streamer = new StreamingDataStream(source, updateProgress);
            return new DataStream(source.Length, streamer.CopyAndReportProgress);
        }

        class StreamingDataStream
        {
            readonly Stream source;
            readonly Action<int> updateProgress;

            public StreamingDataStream(Stream source, Action<int> updateProgress)
            {
                this.source = source;
                this.updateProgress = updateProgress;
            }

            public void CopyAndReportProgress(Stream destination)
            {
                var buffer = new byte[8192];

                var progress = 0;

                int count;
                var totalLength = source.Length;
                long copiedSoFar = 0;
                source.Seek(0, SeekOrigin.Begin);
                while ((count = source.Read(buffer, 0, buffer.Length)) != 0)
                {
                    destination.Write(buffer, 0, count);

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
        }
    }
}