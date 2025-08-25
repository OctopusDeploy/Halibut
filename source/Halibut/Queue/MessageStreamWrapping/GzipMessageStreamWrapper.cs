using System;
using System.IO;
using System.IO.Compression;

namespace Halibut.Queue.MessageStreamWrapping
{
    public class GzipMessageStreamWrapper : IMessageStreamWrapper
    {

        public Stream WrapForWriting(Stream stream)
        {
            return new GZipStream(stream, CompressionMode.Compress);
        }
        
        public Stream WrapForReading(Stream stream)
        {
            return new GZipStream(stream, CompressionMode.Decompress);
        }
    }
}