#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{

    /// <summary>
    /// Helps with safely working with CancellationTokenSources.
    /// 
    /// CancellationTokens and CancellationTokenSources can be tricky to work with since:
    /// - Asking for a token from a disposed CTS throws which is often surprising.
    /// - Disposal of a CTS does not cancel the token.
    /// - Even if the CTS is cancelled then dispose, race conditions exists where some
    /// tasks using the cancelled token DO NOT GET CANCELLED e.g. Task.Delay();
    ///
    /// To help with some of those this class:
    /// - Gets a copy of the Token from the CTS, before it is disposed. So asking
    /// for the token never throws.
    /// - Always cancels the CTS before disposing of it, so anything with the token
    /// general (except in dotnet race condition cases) gets cancelled.
    /// - Supports awaiting tasks that are using the CTS's Token in dispose. Specifically
    /// when disposed this class will cancel the CTS, then await those tasks given to it
    /// (ignoring errors) and only then disposing the CTS. This avoids the bugs/race
    /// conditions in Dotnet.
    /// 
    /// </summary>
    public sealed class CancelOnDisposeCancellationToken : IAsyncDisposable
    {
        readonly CancellationTokenSource cancellationTokenSource;
        bool disposed;

        ConcurrentBag<Task>? tasks = null;
        
        public CancelOnDisposeCancellationToken(params CancellationToken[] token)
            : this(CancellationTokenSource.CreateLinkedTokenSource(token))
        {
        }
        public CancelOnDisposeCancellationToken() : this(new CancellationTokenSource())
        {
        }

        CancelOnDisposeCancellationToken(CancellationTokenSource cancellationTokenSource)
        {
            this.cancellationTokenSource = cancellationTokenSource;
            Token = cancellationTokenSource.Token;
        }

        public CancellationToken Token { get; }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            await Try.IgnoringError(async () => await CancelAsync());

            // Wait for any tasks that are using the token, before disposal
            if (tasks != null)
            {
                await Task.WhenAll(tasks.Select(t => Try.IgnoringError(() => t)));
            }

            Try.IgnoringError(() => cancellationTokenSource.Dispose());
        }

        public async Task CancelAsync()
        {
#if NET8_0_OR_GREATER
            await cancellationTokenSource.CancelAsync();
#else
            await Task.CompletedTask;
            cancellationTokenSource.Cancel();
#endif
        }

        public void CancelAfter(TimeSpan timeSpan)
        {
            cancellationTokenSource.CancelAfter(timeSpan);
        }

        /// <summary>
        /// Tasks supplied here will be awaited on in the dispose method after
        /// the Token is cancelled and before the token is disposed.
        /// Not thread safe. 
        /// </summary>
        /// <param name="tasksUsingToken"></param>
        public void AwaitTasksBeforeCTSDispose(params Task[] tasksUsingToken)
        {
            if (tasks == null)
            {
                Interlocked.CompareExchange(ref tasks, new ConcurrentBag<Task>(), null);
            }
            foreach (var task in tasksUsingToken)
            {
                tasks!.Add(task);
            }
        }
    }
}