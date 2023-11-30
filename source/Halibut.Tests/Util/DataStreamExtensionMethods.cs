using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Util
{
    public static class DataStreamExtensionMethods
    {
        // TODO: This could be async
        public static string ReadAsString(this DataStream stream)
        {
            var result = string.Empty;
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            stream.Receiver().ReadAsync(async (s, ct) =>
                    {
                        using (var reader = new StreamReader(s))
                        {
                            result = await reader.ReadToEndAsync();
                        }
                    },
                    CancellationToken.None)
                .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            return result;
        }
    }
}