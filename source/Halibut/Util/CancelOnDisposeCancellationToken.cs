#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{

    /// <summary>
    /// An async disposable wrapper for CancellationTokenSource that safely cancels and DOES NOT dispose it.
    /// </summary>
    public sealed class CancelOnDisposeCancellationToken : IAsyncDisposable
    {
        readonly CancellationTokenSource cancellationTokenSource;
        bool disposed;
        
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

            // And then don't dispose the CancellationTokenSource.
            // Since doing so WILL result in race conditions where
            // callbacks will be silently not executed.
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
    }
} 