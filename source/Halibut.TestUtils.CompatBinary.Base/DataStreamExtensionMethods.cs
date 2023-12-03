﻿using System;
using System.IO;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public static class DataStreamExtensionMethods
    {
        public static string ReadAsString(this DataStream stream)
        {
            var result = string.Empty;
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
