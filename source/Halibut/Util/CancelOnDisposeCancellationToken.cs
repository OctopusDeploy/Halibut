#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis;
using Halibut.Transport.Protocol;

namespace Halibut.Util
{

    /// <summary>
    /// An async disposable wrapper for CancellationTokenSource that safely cancels and DOES NOT dispose it.
    /// </summary>
    public sealed class CancelOnDisposeCancellationToken : IAsyncDisposable
    {
        readonly CancellationTokenSource cancellationTokenSource;
        bool disposed;

        readonly AwaitAllAndIgnoreException awaitAllAndIgnoreException = new();
        
        public CancelOnDisposeCancellationToken(params CancellationToken[] token)
        : this(CancellationTokenSource.CreateLinkedTokenSource(token))
        {
        }
        public CancelOnDisposeCancellationToken() : this(new CancellationTokenSource())
        {
        }

        private CancelOnDisposeCancellationToken(CancellationTokenSource cancellationTokenSource)
        {
            this.cancellationTokenSource = cancellationTokenSource;
            Token = cancellationTokenSource.Token;
        }

        public CancellationToken Token { get; }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
                return;

            disposed = true;

            await Try.IgnoringError(async () => await CancelAsync());

            // Wait for any tasks that are using the token, before disposal
            await Try.IgnoringError(async () => await awaitAllAndIgnoreException.DisposeAsync());

            Try.IgnoringError(() => cancellationTokenSource.Dispose());
        }

        public async Task CancelAsync()
        {
#if NET8_0_OR_GREATER
            await cancellationTokenSource.CancelAsync();
#else
            CancellationTokenSource.Cancel();
#endif
        }

        public void CancelAfter(TimeSpan timeSpan)
        {
            cancellationTokenSource.CancelAfter(timeSpan);
        }

        public void AwaitTasksBeforeCTSDispose(params Task[] tasksUsingToken)
        {
            awaitAllAndIgnoreException.AddTasks(tasksUsingToken);
        }
    }
} 