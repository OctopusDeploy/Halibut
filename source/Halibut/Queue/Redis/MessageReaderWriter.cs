// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Transport.Protocol;

namespace Halibut.Queue.Redis
{
    public class MessageReaderWriter : IMessageReaderWriter
    {
        readonly QueueMessageSerializer queueMessageSerializer;
        readonly IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues;

        public MessageReaderWriter(QueueMessageSerializer queueMessageSerializer, IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues)
        {
            this.queueMessageSerializer = queueMessageSerializer;
            this.storeDataStreamsForDistributedQueues = storeDataStreamsForDistributedQueues;
        }

        public async Task<string> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var (payload, dataStreams) = queueMessageSerializer.WriteMessage(request);
            await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);
            return payload;
        }
        
        public async Task<RequestMessage> ReadRequest(string jsonRequest, CancellationToken cancellationToken)
        {
            var (request, dataStreams) = queueMessageSerializer.ReadMessage<RequestMessage>(jsonRequest);
            await storeDataStreamsForDistributedQueues.ReHydrateDataStreams(dataStreams, cancellationToken);
            return request;
        }
        
        public async Task<string> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            var (payload, dataStreams) = queueMessageSerializer.WriteMessage(response);
            await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);
            return payload;
        }
        
        public async Task<ResponseMessage> ReadResponse(string jsonResponse, CancellationToken cancellationToken)
        {
            var (response, dataStreams) = queueMessageSerializer.ReadMessage<ResponseMessage>(jsonResponse);
            await storeDataStreamsForDistributedQueues.ReHydrateDataStreams(dataStreams, cancellationToken);
            return response;
        }
    }
}