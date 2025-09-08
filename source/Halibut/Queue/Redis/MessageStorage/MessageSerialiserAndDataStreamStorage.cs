using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.DataStreams;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Transport.Protocol;

namespace Halibut.Queue.Redis.MessageStorage
{
    public class MessageSerialiserAndDataStreamStorage : IMessageSerialiserAndDataStreamStorage
    {
        readonly QueueMessageSerializer queueMessageSerializer;
        readonly IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues;

        public MessageSerialiserAndDataStreamStorage(QueueMessageSerializer queueMessageSerializer, IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues)
        {
            this.queueMessageSerializer = queueMessageSerializer;
            this.storeDataStreamsForDistributedQueues = storeDataStreamsForDistributedQueues;
        }

        public async Task<(RedisStoredMessage, HeartBeatDrivenDataStreamProgressReporter)> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var (jsonRequestMessage, dataStreams) = queueMessageSerializer.WriteMessage(request);
            SwitchDataStreamsToNotReportProgress(dataStreams);
            var dataStreamProgressReporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            var dataStreamMetadata = await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);
            return (new RedisStoredMessage(jsonRequestMessage, dataStreamMetadata), dataStreamProgressReporter);
        }

        static void SwitchDataStreamsToNotReportProgress(IReadOnlyList<DataStream> dataStreams)
        {
            foreach (var dataStream in dataStreams)
            {
                // It doesn't make sense for this side to be reporting progress, since we are not sending the data to tentacle.
                if (dataStream is ICanBeSwitchedToNotReportProgress canBeSwitchedToNotReportProgress)
                {
                    canBeSwitchedToNotReportProgress.SwitchWriterToNotReportProgress();
                }
            }
        }

        public async Task<(RequestMessage, RequestDataStreamsTransferProgress)> ReadRequest(RedisStoredMessage storedMessage, CancellationToken cancellationToken)
        {
            var (request, dataStreams) = queueMessageSerializer.ReadMessage<RequestMessage>(storedMessage.Message);

            var rehydratableDataStreams = BuildUpRehydratableDataStreams(dataStreams, out var dataStreamTransferProgress);

            await storeDataStreamsForDistributedQueues.RehydrateDataStreams(storedMessage.DataStreamMetadata, rehydratableDataStreams, cancellationToken);
            return (request, new RequestDataStreamsTransferProgress(dataStreamTransferProgress));
        }

        public async Task<RedisStoredMessage> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            var (jsonResponseMessage, dataStreams) = queueMessageSerializer.WriteMessage(response);
            var dataStreamMetadata = await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);
            return new RedisStoredMessage(jsonResponseMessage, dataStreamMetadata);
        }
        
        public async Task<ResponseMessage> ReadResponse(RedisStoredMessage storedMessage, CancellationToken cancellationToken)
        {
            var (response, dataStreams) = queueMessageSerializer.ReadMessage<ResponseMessage>(storedMessage.Message);
            
            var rehydratableDataStreams = BuildUpRehydratableDataStreams(dataStreams, out _);
            
            await storeDataStreamsForDistributedQueues.RehydrateDataStreams(storedMessage.DataStreamMetadata, rehydratableDataStreams, cancellationToken);
            return response;
        }
        
        static List<IRehydrateDataStream> BuildUpRehydratableDataStreams(IReadOnlyList<DataStream> dataStreams, out List<RedisDataStreamTransferProgressRecorder> dataStreamTransferProgress)
        {
            var rehydratableDataStreams = new List<IRehydrateDataStream>();
            dataStreamTransferProgress = new List<RedisDataStreamTransferProgressRecorder>();
            foreach (var dataStream in dataStreams)
            {
                var dtp = new RedisDataStreamTransferProgressRecorder(dataStream);
                dataStreamTransferProgress.Add(dtp);
                rehydratableDataStreams.Add(new RehydrateWithProgressReporting(dataStream, dtp));
            }

            return rehydratableDataStreams;
        }
    }
    

    public class RequestDataStreamsTransferProgress
    {
        public IReadOnlyList<RedisDataStreamTransferProgressRecorder> TransferProgress { get; }

        public RequestDataStreamsTransferProgress(List<RedisDataStreamTransferProgressRecorder> transferProgress)
        {
            this.TransferProgress = transferProgress;
        }
    }

    public interface IRehydrateDataStream
    {
        public Guid Id { get; }
        public long Length { get; }
        
        // Rehydrates the datastream with the stream returned by data.
        // Once that stream is returned that stream will be disposed as well
        // as the disposable if not null.
        public void Rehydrate(Func<(Stream, IAsyncDisposable?)> data);
    }

    public class RehydrateWithProgressReporting : IRehydrateDataStream
    {
        DataStream dataStream;
        IDataStreamTransferProgress dataStreamTransferProgress;

        public RehydrateWithProgressReporting(DataStream dataStream, IDataStreamTransferProgress dataStreamTransferProgress)
        {
            this.dataStream = dataStream;
            this.dataStreamTransferProgress = dataStreamTransferProgress;
        }

        public Guid Id => dataStream.Id;
        public long Length => dataStream.Length;

        public void Rehydrate(Func<(Stream, IAsyncDisposable?)> data)
        {
            
            dataStream.SetWriterAsync(async (destination, ct) =>
            {
                var sourceAndDisposable = data();
                await using var source = sourceAndDisposable.Item1;
                try
                {
                    var streamCopier = new StreamCopierWithProgress(source, dataStreamTransferProgress);
                    await streamCopier.CopyAndReportProgressAsync(destination, ct);
                }
                finally
                {
                    if(sourceAndDisposable.Item2 != null) await sourceAndDisposable.Item2.DisposeAsync();
                }
            });
        }
    }
}