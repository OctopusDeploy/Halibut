using System;

namespace Halibut.Tests.Util
{
    public class DataStreamUtil
    {
        public static DataStream From(string firstSend, Action andThenRun, string thenSend)
        {
            var helloBytes = firstSend.GetBytesUtf8();
            var allDoneBytes = thenSend.GetBytesUtf8();
            return new DataStream(helloBytes.Length + allDoneBytes.Length,
                stream =>
                {
                    stream.Write(helloBytes, 0, helloBytes.Length);
                    stream.Flush();
                    andThenRun();
                    stream.Write(allDoneBytes, 0, allDoneBytes.Length);
                    stream.Flush();
                },
                async (stream, ct) =>
                {
                    await stream.WriteAsync(helloBytes, 0, helloBytes.Length, ct);
                    await stream.FlushAsync(ct);
                    andThenRun();
                    await stream.WriteAsync(allDoneBytes, 0, allDoneBytes.Length, ct);
                    await stream.FlushAsync(ct);
                });
        }
    }
}