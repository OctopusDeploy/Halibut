#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{
    public static class CancellationTokenSourceExtensionMethods
    {
        /// <summary>
        /// Creates an async disposable wrapper for the CancellationTokenSource that will safely cancel and dispose it.
        /// </summary>
        /// <param name="cancellationTokenSource">The CancellationTokenSource to wrap</param>
        /// <returns>An IAsyncDisposable that will cancel and dispose the CancellationTokenSource</returns>
        public static CancelOnDisposeCancellationTokenSource CancelOnDispose(this CancellationTokenSource cancellationTokenSource)
        {
            return new CancelOnDisposeCancellationTokenSource(cancellationTokenSource);
        }
    }

    /// <summary>
    /// An async disposable wrapper for CancellationTokenSource that safely cancels and disposes it.
    /// </summary>
    public sealed class CancelOnDisposeCancellationTokenSource : IAsyncDisposable
    {
        public readonly CancellationTokenSource CancellationTokenSource;
        private bool disposed = false;

        internal CancelOnDisposeCancellationTokenSource(CancellationTokenSource cancellationTokenSource)
        {
            this.CancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
            this.CancellationToken = cancellationTokenSource.Token;
        }

        public CancellationToken CancellationToken { get; }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
                return;

            disposed = true;

            // First, attempt to cancel the cancellation token source
#if NET8_0_OR_GREATER
            await Try.IgnoringError(async () => await CancellationTokenSource.CancelAsync());
#else
            Try.IgnoringError(() => CancellationTokenSource.Cancel());
#endif

            // Then, dispose the cancellation token source
            Try.IgnoringError(() => CancellationTokenSource.Dispose());
        }
    }
} 