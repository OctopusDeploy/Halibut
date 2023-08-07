using System;
using System.Collections;
using Halibut.Tests.Support.TestCases;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class SyncAndAsyncTestCasesAttribute : TestCaseSourceAttribute
    {
        public SyncAndAsyncTestCasesAttribute(string value)
            : base(typeof(TestCases), nameof(TestCases.GetEnumerator), new[] { value })
        {
        }

        static class TestCases
        {
            internal static IEnumerable GetEnumerator(string value)
            {
                yield return new SyncAndAsyncTestCase(SyncOrAsync.Async, value);
                yield return new SyncAndAsyncTestCase(SyncOrAsync.Sync, value);
            }
        }
    }
}
