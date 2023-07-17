using System;
using System.IO;

namespace Halibut.TestUtils.Contracts
{
    public static class DataStreamExtensionMethods
    {
        public static string ReadAsString(this DataStream stream)
        {
            string result = null;
            stream.Receiver().Read(s =>
            {
                using (var reader = new StreamReader(s))
                {
                    result = reader.ReadToEnd();
                }
            });
            return result;
        }
    }
}
