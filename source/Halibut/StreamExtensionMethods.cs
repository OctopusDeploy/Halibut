
#nullable enable
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Halibut.Transport;

namespace Halibut
{
    public static class StreamExtensionMethods
    {
        public static void WriteStringToStream(this FileStream stream, string s)
        {
            var b = s.ToUtf8();
            stream.Write(b, 0, b.Length);
            stream.Flush();
        }
        
        public static bool TryTakeNote(this Stream stream, string msg, OnStream onStream)
        {
            // StreamAndRecord streamAndRecord = null;
            // if (stream is RewindableBufferStream)
            // {
            //     var rewindableBufferStream = (RewindableBufferStream) stream;
            //     var fieldName = "baseStream";
            //     var field = TryGetInnerStream(stream, fieldName);
            //     TryTakeNote(rewindableBufferStream.baseStream, msg, onStream);
            //
            // }
            if (stream is StreamAndRecord)
            {
                var streamAndRecord = (StreamAndRecord) stream;
                streamAndRecord.MakeNote(msg, onStream);
                return true;
            }

            var fieldsToLookFor = new string[] {"baseStream", "inner", "_stream"};
            foreach (var fieldName in fieldsToLookFor)
            {
                var inner = TryGetInnerStream(stream, fieldName);
                if (inner != null)
                {
                    if (TryTakeNote(inner, msg, onStream)) return true;
                }
            }

            return false;
        }

        static Stream? TryGetInnerStream(Stream stream, string fieldName)
        {
            var fieldInfo = stream.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null)
            {
                return null;
            }
            var value = fieldInfo.GetValue(stream);
            var res = value as Stream;
            return res;
        }
    }
}
#nullable disable