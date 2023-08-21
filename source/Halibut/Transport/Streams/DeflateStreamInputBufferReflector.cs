#nullable enable

using System;
using System.IO.Compression;
using System.Reflection;
using Halibut.Diagnostics;

namespace Halibut.Transport.Streams
{
    /// <summary>
    /// Reflects <see cref="DeflateStream"/> to determine the number of bytes available in an instance's input buffer.
    /// </summary>
    /// <remarks>
    /// When <see cref="DeflateStream"/> fills its buffer, it can consume uncompressed bytes from the stream that appear after
    /// the compressed bytes. Once deflation is complete, any bytes left in the internal zlib stream buffer are these uncompressed bytes.
    ///
    /// Knowing the number of over-consumed bytes is important, because Halibut protocol control messages may have been inadvertently consumed.
    /// See: https://github.com/OctopusDeploy/Halibut/pull/154
    /// </remarks>
    class DeflateStreamInputBufferReflector
    {
        readonly FieldInfo? inflaterFieldCached;
        readonly FieldInfo? zlibStreamFieldCached;
        readonly PropertyInfo? availInPropertyCached;
        ILog log;

        public DeflateStreamInputBufferReflector(ILog log)
        {
            this.log = log;
            inflaterFieldCached = typeof(DeflateStream).GetField("_inflater", BindingFlags.NonPublic | BindingFlags.Instance);
            if (inflaterFieldCached == null) inflaterFieldCached = typeof(DeflateStream).GetField("inflater", BindingFlags.NonPublic | BindingFlags.Instance);

            // Only in net6 will this work
            zlibStreamFieldCached = inflaterFieldCached?.FieldType.GetField("_zlibStream", BindingFlags.NonPublic | BindingFlags.Instance);
            availInPropertyCached = zlibStreamFieldCached?.FieldType.GetProperty("AvailIn");
        }

        public bool TryGetAvailableInputBufferSize(DeflateStream stream, out uint inputBufferAvailSize)
        {
            try
            {
                return _TryGetAvailableInputBufferSize(stream, out inputBufferAvailSize);
            }
            catch (Exception e)
            {
                log.Write(EventType.Error, "Could not find internal buffer size field.", e);
                inputBufferAvailSize = 0;
                return false;
            }
        }
        bool _TryGetAvailableInputBufferSize(DeflateStream stream, out uint inputBufferAvailSize)
        {
            inputBufferAvailSize = 0;

            if (inflaterFieldCached == null) return false;

            var inflater = inflaterFieldCached.GetValue(stream);
            if (inflater == null) return false;

            // in Net48 we need to look at the actual inflater object since otherwise we are looking at a interface with no zlibstream.
            var zlibStreamField = zlibStreamFieldCached ?? inflater.GetType().GetField("_zlibStream", BindingFlags.NonPublic | BindingFlags.Instance);
            if (zlibStreamField == null) return false;

            var zlibStream = zlibStreamField.GetValue(inflater);
            if (zlibStream is null) return false;

            var availInProperty = availInPropertyCached ?? zlibStream.GetType().GetProperty("AvailIn");
            if (availInProperty == null) return false;

            var size = (uint?)availInProperty.GetValue(zlibStream);
            if (size is null) return false;

            inputBufferAvailSize = size.Value;
            return true;
        }
    }
}
