using System.Text;

namespace Halibut.Tests.Support
{
    public static class StringExtensionMethods
    {
        public static byte[] GetUTF8Bytes(this string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }
    }
}