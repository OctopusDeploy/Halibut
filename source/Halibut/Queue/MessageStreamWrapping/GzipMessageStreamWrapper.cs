using System;
using System.IO;
using System.IO.Compression;

namespace Halibut.Queue.MessageStreamWrapping
{
    /// <summary>
    /// Example implementation of IMessageStreamWrapper, which compresses
    /// on serilisation and decompresses when deserialising Request/Response
    /// messages.
    /// </summary>
    public class GzipMessageStreamWrapper : IMessageStreamWrapper
    {

        public Stream WrapMessageSerialisationStream(Stream stream)
        {
            return new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
        }
        
        public Stream WrapMessageDeserialisationStream(Stream stream)
        {
            return new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        }
    }
}