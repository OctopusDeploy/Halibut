using System;
using System.IO;
using System.Threading;
using Halibut.Diagnostics;

namespace Halibut
{
    public static class StreamRecorderWrapperHelper
    {
        public static string Instance = "test"; //(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds + "";

        static int counter = 0;
        public static string StreamName()
        {
            int n = Interlocked.Increment(ref counter);
            return $"{Instance}_{n}";
        }

        public static Stream ToRecordingStream(this Stream stream, string name, ILog log)
        {
            var dir = streamDumpDir;
            if(string.IsNullOrEmpty(dir)) dir = Environment.GetEnvironmentVariable("halibut_streams_dump_dir");
            if(string.IsNullOrEmpty(dir)) return stream;

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var streamName = StreamName();
            log.Write(EventType.Error, "Creating stream " + streamName);
            
            var s = new StreamAndRecord(stream, dir + streamName, name);
            
            return s;
        }

        public static string streamDumpDir = null;
        
        
    }
    public class StreamAndRecord : Stream
    {
        public Stream stream;
        FileStream writtenStream;
        FileStream readStream;
        FileStream otherStream;
        string name;

        public StreamAndRecord(Stream stream, string recordStem, string name)
        {
            this.stream = stream;
            this.name = name;

            writtenStream = File.Create(recordStem + "_written" + "_" + name + ".txt");
            readStream = File.Create(recordStem + "_read" + "_" + name + ".txt");
            otherStream = File.Create(recordStem + "_other" + "_" + name + ".txt");
        }

        public override void Flush()
        {
            otherStream.WriteStringToStream("Flushed\n");
            stream.Flush();
        }

        int readCount = 0;

        public void MakeNote(string note, OnStream stream)
        {
            otherStream.WriteStringToStream(note);
            switch (stream)
            {
                case OnStream.WRITE:
                    writtenStream.WriteStringToStream(note);
                    break;
                case OnStream.READ:
                    readStream.WriteStringToStream(note);
                    break;
            }
            
            
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            otherStream.WriteStringToStream("Reading\n");
            int read = stream.Read(buffer, offset, count);
            otherStream.WriteStringToStream("Read\n");

            if (read > 0)
            {
                readCount += read;
                readStream.Write(buffer, offset, read);
                readStream.Flush();
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            otherStream.WriteStringToStream($"Seek to {offset}\r\n");
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            otherStream.WriteStringToStream($"Set length to {value}\r\n");
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            writtenStream.Write(buffer, offset, count);
            writtenStream.Flush();
            otherStream.WriteStringToStream("Writing\n");
            stream.Write(buffer, offset, count);
            otherStream.WriteStringToStream("Written\n");
        }

        public override void Close()
        {
            try
            {
                writtenStream.Close();
                readStream.Close();
                otherStream.Close();
            }
            catch (Exception)
            {
            }
            base.Close();
        }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }
    }

    public enum OnStream
    {
        READ,
        WRITE
    }
}