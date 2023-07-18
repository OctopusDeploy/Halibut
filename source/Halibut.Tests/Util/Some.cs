using System;
using System.Text;

namespace Halibut.Tests.Util
{
    public static class Some
    {
        public static string RandomAsciiStringOfLength(int length)
        {
            var sb = new StringBuilder(length);
            var random = new Random();
            const int minPrintableCharacter = 32;
            const int maxPrintableCharacter = 126;
            const int availableChars = maxPrintableCharacter - minPrintableCharacter;
            
            for (int i = 0; i < length; i++)
            {
                var nextByte = (char) (random.Next(availableChars) + minPrintableCharacter);
                sb.Append(nextByte);

            }

            return sb.ToString();
        }
    }
}