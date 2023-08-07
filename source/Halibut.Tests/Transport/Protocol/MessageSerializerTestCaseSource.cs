using System.Collections;
using Halibut.Tests.Support.TestAttributes;

namespace Halibut.Tests.Transport.Protocol
{
    public class MessageSerializerTestCase
    {
        public SyncOrAsync SyncOrAsync { get; }
        public long AsyncMemoryLimit { get; }

        public MessageSerializerTestCase(SyncOrAsync syncOrAsync, long asyncMemoryLimit)
        {
            SyncOrAsync = syncOrAsync;
            AsyncMemoryLimit = asyncMemoryLimit;
        }

        public override string ToString() => $"{SyncOrAsync}, Memory Limit {AsyncMemoryLimit}";
    }

    public class MessageSerializerTestCaseSource : IEnumerable
    {
        const long SmallMemoryLimit = 8L;
        const long LargeMemoryLimit = 16L * 1024L * 1024L;

        public IEnumerator GetEnumerator()
        {
            yield return new MessageSerializerTestCase(SyncOrAsync.Sync, 0);
            yield return new MessageSerializerTestCase(SyncOrAsync.Async, SmallMemoryLimit);
            yield return new MessageSerializerTestCase(SyncOrAsync.Async, LargeMemoryLimit);
        }
    }
}