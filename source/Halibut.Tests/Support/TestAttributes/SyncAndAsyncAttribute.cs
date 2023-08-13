using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Streams;
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
        public static SyncOrAsyncWithoutResult WhenSync(this SyncOrAsync syncOrAsync, Action action)
        {
            if (syncOrAsync == SyncOrAsync.Sync)
            {
                action();
            }

            return new(syncOrAsync);
        }
        
        public static async Task WhenAsync(this SyncOrAsyncWithoutResult syncOrAsyncWithoutResult, Func<Task> action)
        {
            if (syncOrAsyncWithoutResult.SyncOrAsync == SyncOrAsync.Async)
            {
                await action();
            }
        }

        public static void WhenAsync(this SyncOrAsyncWithoutResult syncOrAsyncWithoutResult, Action action)
        {
            if (syncOrAsyncWithoutResult.SyncOrAsync == SyncOrAsync.Async)
            {
                action();
            }
        }

        public static SyncOrAsyncWithResult<T> WhenSync<T>(this SyncOrAsync syncOrAsync, Func<T> action)
        {
            if (syncOrAsync == SyncOrAsync.Sync)
            {
                return new SyncOrAsyncWithResult<T>(syncOrAsync, action());
            }

            return new SyncOrAsyncWithResult<T>(syncOrAsync, default);
        }

        public static SyncOrAsyncWithoutResult IgnoreResult<T>(this SyncOrAsyncWithResult<T> syncOrAsyncWithResult)
        {
            return new SyncOrAsyncWithoutResult(syncOrAsyncWithResult.SyncOrAsync);
        }

        public static async Task<T> WhenAsync<T>(this SyncOrAsyncWithResult<T> syncOrAsyncWithResult, Func<Task<T>> action)
        {
            if (syncOrAsyncWithResult.SyncOrAsync == SyncOrAsync.Async)
            {
                return await action();
            }

            return syncOrAsyncWithResult.Result!;
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

        public static async Task WriteToStream(this SyncOrAsync syncOrAsync, Stream stream, byte[] bytes)
        {
            await syncOrAsync.WhenSync(() => stream.WriteByteArray(bytes))
                .WhenAsync(() => stream.WriteByteArrayAsync(bytes, CancellationToken.None));
        }
        
        public static async Task<int> ReadFromStream(this SyncOrAsync syncOrAsync, Stream stream, byte[] bytes, int offset, int count)
        {
            return await syncOrAsync.WhenSync(() => stream.Read(bytes, offset, count))
                .WhenAsync(() => stream.ReadAsync(bytes, offset, count, CancellationToken.None));
        }
    }

    public class SyncOrAsyncWithoutResult
    {
        public SyncOrAsync SyncOrAsync { get; }

        public SyncOrAsyncWithoutResult(SyncOrAsync syncOrAsync)
        {
            SyncOrAsync = syncOrAsync;
        }
    }

    public class SyncOrAsyncWithResult<T>
    {
        public SyncOrAsync SyncOrAsync { get; }
        public T? Result{ get; }

        public SyncOrAsyncWithResult(SyncOrAsync syncOrAsync, T? result)
        {
            SyncOrAsync = syncOrAsync;
            Result = result;
        }
    }
}