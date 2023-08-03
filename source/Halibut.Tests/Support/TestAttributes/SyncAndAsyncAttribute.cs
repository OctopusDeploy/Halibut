using System;
using System.Collections;
using System.Threading.Tasks;
using Halibut.Util;
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

        public static SyncOrAsyncAndResult<T> WhenSync<T>(this SyncOrAsync syncOrAsync, Func<T> action)
        {
            if (syncOrAsync == SyncOrAsync.Sync)
            {
                return new SyncOrAsyncAndResult<T>(syncOrAsync, action());
            }

            return new SyncOrAsyncAndResult<T>(syncOrAsync, default);
        }

        public static async Task<T> WhenAsync<T>(this SyncOrAsyncAndResult<T> syncOrAsyncAndResult, Func<Task<T>> action)
        {
            if (syncOrAsyncAndResult.SyncOrAsync == SyncOrAsync.Async)
            {
                return await action();
            }

            return syncOrAsyncAndResult.Result!;
        }

        public static AsyncHalibutFeature ToAsyncHalibutFeature(this SyncOrAsync syncOrAsync)
        {
            return syncOrAsync switch
            {
                SyncOrAsync.Sync => AsyncHalibutFeature.Disabled,
                SyncOrAsync.Async => AsyncHalibutFeature.Enabled,
                _ => throw new ArgumentOutOfRangeException(nameof(syncOrAsync), syncOrAsync, null)
            };
        }
    }

    public class SyncOrAsyncAndResult<T>
    {
        public SyncOrAsync SyncOrAsync { get; }
        public T? Result{ get; }

        public SyncOrAsyncAndResult(SyncOrAsync syncOrAsync, T? result)
        {
            SyncOrAsync = syncOrAsync;
            Result = result;
        }
    }
}