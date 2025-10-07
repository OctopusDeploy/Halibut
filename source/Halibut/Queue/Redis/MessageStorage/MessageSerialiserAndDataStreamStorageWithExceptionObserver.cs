using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Transport.Protocol;

namespace Halibut.Queue.Redis.MessageStorage
{
    /// <summary>
    /// Decorator implementation of IMessageSerialiserAndDataStreamStorage that wraps another implementation
    /// and notifies an observer when exceptions occur during any of the operations.
    /// </summary>
    public class MessageSerialiserAndDataStreamStorageWithExceptionObserver : IMessageSerialiserAndDataStreamStorage
    {
        readonly IMessageSerialiserAndDataStreamStorage inner;
        readonly IMessageSerialiserAndDataStreamStorageExceptionObserver exceptionObserver;

        public MessageSerialiserAndDataStreamStorageWithExceptionObserver(
            IMessageSerialiserAndDataStreamStorage inner, 
            IMessageSerialiserAndDataStreamStorageExceptionObserver exceptionObserver)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.exceptionObserver = exceptionObserver ?? throw new ArgumentNullException(nameof(exceptionObserver));
        }

        public async Task<(RedisStoredMessage, HeartBeatDrivenDataStreamProgressReporter)> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return await inner.PrepareRequest(request, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptionObserver.OnException(ex, nameof(PrepareRequest));
                throw;
            }
        }

        public async Task<(RequestMessage, RequestDataStreamsTransferProgress)> ReadRequest(RedisStoredMessage jsonRequest, CancellationToken cancellationToken)
        {
            try
            {
                return await inner.ReadRequest(jsonRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptionObserver.OnException(ex, nameof(ReadRequest));
                throw;
            }
        }

        public async Task<RedisStoredMessage> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                return await inner.PrepareResponse(response, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptionObserver.OnException(ex, nameof(PrepareResponse));
                throw;
            }
        }

        public async Task<ResponseMessage> ReadResponse(RedisStoredMessage jsonResponse, CancellationToken cancellationToken)
        {
            try
            {
                return await inner.ReadResponse(jsonResponse, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptionObserver.OnException(ex, nameof(ReadResponse));
                throw;
            }
        }
    }
}

