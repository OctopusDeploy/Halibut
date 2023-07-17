using System;

namespace Halibut.Tests.Util
{
    public class DataStreamUtil
    {
        public static DataStream From(string firstSend, Action andThenRun, string thenSend)
        {
            var helloBytes = "hello".GetBytesUtf8();
            var allDoneBytes = "All done".GetBytesUtf8();
            return new DataStream(helloBytes.Length + allDoneBytes.Length, stream =>
            {
                stream.Write(helloBytes);
                stream.Flush();
                andThenRun();
                stream.Write(allDoneBytes);
                stream.Flush();
            });
        }
    }
}