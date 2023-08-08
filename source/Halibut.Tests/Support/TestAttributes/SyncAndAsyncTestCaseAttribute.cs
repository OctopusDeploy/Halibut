using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class SyncAndAsyncTestCaseAttribute : TestCaseSourceAttribute
    {
        public SyncAndAsyncTestCaseAttribute(params object[] parameters)
            : base(typeof(SyncAndAsyncTestCase), nameof(SyncAndAsyncTestCase.GetTestCases), new [] { parameters })
        {
        }

        static class SyncAndAsyncTestCase
        {
            internal static IEnumerable GetTestCases(object[] parameters)
            {
                yield return parameters.Append(SyncOrAsync.Sync).ToArray();
                yield return parameters.Append(SyncOrAsync.Async).ToArray();
            }
        }
    }
}
