using System.Collections;

namespace Halibut.Tests.Transport.Protocol
{
    public class MessageSerializerTestCase
    {
        public long AsyncMemoryLimit { get; }

        public MessageSerializerTestCase(long asyncMemoryLimit)
        {
            AsyncMemoryLimit = asyncMemoryLimit;
        }

        public override string ToString() => $"Memory Limit {AsyncMemoryLimit}";
    }

    public class MessageSerializerTestCaseSource : IEnumerable
    {
        const long SmallMemoryLimit = 8L;
        const long LargeMemoryLimit = 16L * 1024L * 1024L;

        public IEnumerator GetEnumerator()
        {
            yield return new MessageSerializerTestCase(SmallMemoryLimit);
            yield return new MessageSerializerTestCase(LargeMemoryLimit);
        }
    }
}