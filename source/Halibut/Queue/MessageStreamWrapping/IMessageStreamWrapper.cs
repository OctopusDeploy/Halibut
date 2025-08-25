using System;
using System.IO;

namespace Halibut.Queue.MessageStreamWrapping
{
    public interface IMessageStreamWrapper {
    
        /// <summary>
        /// Wraps the stream the messages are serialised to.
        ///
        /// An implementation of this might be a stream that compresses data given to it.
        /// 
        /// The resulting stream must leaveOpen the given stream on dispose
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        Stream WrapForWriting(Stream stream);
        
        /// <summary>
        /// Wraps the streams the messages are deserialised from.
        ///
        /// An implmentation of this might be a stream that decompresses data.
        ///
        /// The resulting stream must leaveOpen the given stream on dispose.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        Stream WrapForReading(Stream stream);
    }
}