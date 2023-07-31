using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class SyncAndAsyncAttribute : TestCaseSourceAttribute
    {
        public SyncAndAsyncAttribute() : base(
            typeof(TestCases),
            nameof(TestCases.GetEnumerator))
        {
        }

        static class TestCases
        {
            public static IEnumerable GetEnumerator()
            {
                yield return SyncOrAsync.Sync;
                yield return SyncOrAsync.Async;
            }
        }
    }

    public enum SyncOrAsync
    {
        Sync,
        Async
    }

    public static class SyncOrAsyncExtensions
    {
        public static SyncOrAsync WhenSync(this SyncOrAsync syncOrAsync, Action action)
        {
            if (syncOrAsync == SyncOrAsync.Sync)
            {
                action();
            }

            return syncOrAsync;
        }

        public static async Task WhenAsync(this SyncOrAsync syncOrAsync, Func<Task> action)
        {
            if (syncOrAsync == SyncOrAsync.Async)
            {
                await action();
            }
        }
    }
}