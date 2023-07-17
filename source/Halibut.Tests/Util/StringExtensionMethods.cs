using System.IO;
using System.Text;

namespace Halibut.Tests.Util
{
    public static class StringExtensionMethods
    {
        public static byte[] GetBytesUtf8(this string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }
    }
}