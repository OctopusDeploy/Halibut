using System.IO;

namespace Halibut.Tests.Util
{
    public static class StreamExtensionMethods
    {
        public static void WriteString(this Stream stream, string s)
        {
            var bytes = s.GetBytesUtf8();
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}