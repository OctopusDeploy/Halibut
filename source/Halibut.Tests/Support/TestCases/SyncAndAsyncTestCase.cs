using System;
using Halibut.Tests.Support.TestAttributes;

namespace Halibut.Tests.Support.TestCases
{
    public class SyncAndAsyncTestCase
    {
        public SyncAndAsyncTestCase(SyncOrAsync syncOrAsync, string value)
        {
            SyncOrAsync = syncOrAsync;
            Value = value;
        }

        public SyncOrAsync SyncOrAsync { get; }
        public string Value { get; }

        public override string ToString()
        {
            return $"{SyncOrAsync}, {Value}";
        }
    }
}
