using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public static class MessageReaderWriterExtensionsMethods
    {
        public static IMessageSerialiserAndDataStreamStorage ThrowsOnReadResponse(this IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage, Func<Exception> exceptionFactory)
        {
            return new MessageSerialiserAndDataStreamStorageThatThrowsWhenReadingResponse(messageSerialiserAndDataStreamStorage, exceptionFactory);
        }

        public static IMessageSerialiserAndDataStreamStorage ThrowsOnPrepareRequest(this IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage, Func<Exception> exception)
        {
            return new MessageSerialiserAndDataStreamStorageThatThrowsOnPrepareRequest(messageSerialiserAndDataStreamStorage, exception);
        }
    }

    class MessageSerialiserAndDataStreamStorageWithVirtualMethods : IMessageSerialiserAndDataStreamStorage
    {
        readonly IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage;

        public MessageSerialiserAndDataStreamStorageWithVirtualMethods(IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage)
        {
            this.messageSerialiserAndDataStreamStorage = messageSerialiserAndDataStreamStorage;
        }

        public virtual Task<(RedisStoredMessage, HeartBeatDrivenDataStreamProgressReporter)> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            return messageSerialiserAndDataStreamStorage.PrepareRequest(request, cancellationToken);
        }

        public virtual Task<(PreparedRequestMessage, RequestDataStreamsTransferProgress)> ReadRequest(RedisStoredMessage jsonRequest, CancellationToken cancellationToken)
        {
            return messageSerialiserAndDataStreamStorage.ReadRequest(jsonRequest, cancellationToken);
        }

        public virtual Task<RedisStoredMessage> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            return messageSerialiserAndDataStreamStorage.PrepareResponse(response, cancellationToken);
        }

        public virtual Task<ResponseMessage> ReadResponse(RedisStoredMessage jsonResponse, CancellationToken cancellationToken)
        {
            return messageSerialiserAndDataStreamStorage.ReadResponse(jsonResponse, cancellationToken);
        }
    }

    class MessageSerialiserAndDataStreamStorageThatThrowsWhenReadingResponse : MessageSerialiserAndDataStreamStorageWithVirtualMethods
    {
        readonly Func<Exception> exception;

        public MessageSerialiserAndDataStreamStorageThatThrowsWhenReadingResponse(IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage, Func<Exception> exception) : base(messageSerialiserAndDataStreamStorage)
        {
            this.exception = exception;
        }

        public override Task<ResponseMessage> ReadResponse(RedisStoredMessage jsonResponse, CancellationToken cancellationToken)
        {
            throw exception();
        }
    }
    
    class MessageSerialiserAndDataStreamStorageThatThrowsOnPrepareRequest : MessageSerialiserAndDataStreamStorageWithVirtualMethods
    {
        readonly Func<Exception> exception;

        public MessageSerialiserAndDataStreamStorageThatThrowsOnPrepareRequest(IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage, Func<Exception> exception) : base(messageSerialiserAndDataStreamStorage)
        {
            this.exception = exception;
        }

        public override Task<(RedisStoredMessage, HeartBeatDrivenDataStreamProgressReporter)> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            throw exception();
        }
    }
}