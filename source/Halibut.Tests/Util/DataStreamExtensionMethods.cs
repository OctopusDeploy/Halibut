using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Util
{
    public static class DataStreamExtensionMethods
    {
        public static async Task<string> ReadAsString(this DataStream stream, CancellationToken cancellationToken)
        {
            var result = string.Empty;
            await stream.Receiver().ReadAsync(async (s, ct) =>
                    {
                        using (var reader = new StreamReader(s))
                        {
                            result = await reader.ReadToEndAsync();
                        }
                    },
                    cancellationToken);
            return result;
        }
    }
}