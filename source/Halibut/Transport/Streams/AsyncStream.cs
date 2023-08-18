#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;

namespace Halibut.Transport.Streams
{
    /// <summary>
    ///     A Stream which helps with implementing everything that is required to make a stream async.
    ///     This will force all required async methods to be implemented.
    ///     This will also implement the old style async programing APM to go to the equivalent
    ///     TAM (ie normal async) ReadAsync/WriteAsync method.
    ///     This also ensures a async disposable method exists on the stream even in net48.
    ///     net48 Stream does not implement IAsyncDisposable.
    /// </summary>
    public abstract class AsyncStream : AsyncDisposableStream
    {
        public sealed override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await _ReadAsync(buffer, offset, count, cancellationToken);
        }

        protected abstract Task<int> _ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

        public sealed override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _WriteAsync(buffer, offset, count, cancellationToken);
        }

        protected abstract Task _WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

        public sealed override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _FlushAsync(cancellationToken);
        }

        protected abstract Task _FlushAsync(CancellationToken cancellationToken);

#if NETFRAMEWORK
        public sealed override ValueTask DisposeAsync()
        {
            return _DisposeAsync();
        }
#else
        public sealed override ValueTask DisposeAsync() => _DisposeAsync();
#endif
        protected abstract ValueTask _DisposeAsync();

        /**
         * Ensures that calls to old APM-style async methods are redirected
         * to new TAP-style async methods.
         */
        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            // BeginRead does not respect timeouts. So force it to use ReadAsync, which does.
            // Redirect to ReadAsync to ensure code execution stays async.
            return _ReadAsync(buffer, offset, count, CancellationToken.None).AsAsynchronousProgrammingModel(callback, state);
        }

        public sealed override int EndRead(IAsyncResult asyncResult)
        {
            try
            {
                return ((Task<int>) asyncResult).Result;
            }
            catch (AggregateException e) when (e.InnerExceptions.Count == 1 && e.InnerException is not null)
            {
                throw e.InnerException;
            }
        }

        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            // BeginWrite does not respect timeouts. So force it to use ReadAsync, which does.
            // Redirect to BeginWrite to ensure code execution stays async.
            return _WriteAsync(buffer, offset, count, CancellationToken.None).AsAsynchronousProgrammingModel(callback, state);
        }

        public sealed override void EndWrite(IAsyncResult asyncResult)
        {
            var task = (Task) asyncResult;
            try
            {
                Task.WaitAll(task);
            }
            catch (AggregateException e) when (e.InnerExceptions.Count == 1 && e.InnerException is not null)
            {
                throw e.InnerException;
            }
        }
    }
}