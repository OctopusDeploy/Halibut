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

    }
}