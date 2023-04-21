using System.IO;
using System.Text;

namespace Halibut
{
    public static class StringExtensionMethods
    {

        public static byte[] ToUtf8(this string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }

        public static void WriteStringToStream(this FileStream stream, string s)
        {
            var b = s.ToUtf8();
            stream.Write(b, 0, b.Length);
            stream.Flush();
        }
        
    }
}