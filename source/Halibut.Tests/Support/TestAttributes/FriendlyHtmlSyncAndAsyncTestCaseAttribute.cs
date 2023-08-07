using System;
using System.Collections;
using Halibut.Tests.Support.TestCases;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class FriendlyHtmlSyncAndAsyncTestCasesAttribute : TestCaseSourceAttribute
    {
        public FriendlyHtmlSyncAndAsyncTestCasesAttribute(string html, string expected)
            : base(typeof(TestCases), nameof(TestCases.GetEnumerator), new[] { html, expected })
        {
        }

        static class TestCases
        {
            internal static IEnumerable GetEnumerator(string html, string expected)
            {
                yield return new FriendlyHtmlSyncAndAsyncTestCase
                {
                    SyncOrAsync = SyncOrAsync.Sync,
                    Html = html,
                    Expected = expected
                };

                yield return new FriendlyHtmlSyncAndAsyncTestCase
                {
                    SyncOrAsync = SyncOrAsync.Async,
                    Html = html,
                    Expected = expected
                };
            }
        }
    }
}
