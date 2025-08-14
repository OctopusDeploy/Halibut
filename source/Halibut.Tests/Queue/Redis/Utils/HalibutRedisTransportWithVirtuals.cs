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

using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis;
using StackExchange.Redis;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class HalibutRedisTransportWithVirtuals : IHalibutRedisTransport
    {
        readonly IHalibutRedisTransport halibutRedisTransport;

        public HalibutRedisTransportWithVirtuals(IHalibutRedisTransport halibutRedisTransport)
        {
            this.halibutRedisTransport = halibutRedisTransport;
        }

        public Task<IAsyncDisposable> SubscribeToRequestMessagePulseChannel(Uri endpoint, Action<ChannelMessage> onRequestMessagePulse, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToRequestMessagePulseChannel(endpoint, onRequestMessagePulse, cancellationToken);
        }

        public Task PulseRequestPushedToEndpoint(Uri endpoint, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PulseRequestPushedToEndpoint(endpoint, cancellationToken);
        }

        public Task PushRequestGuidOnToQueue(Uri endpoint, Guid guid, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PushRequestGuidOnToQueue(endpoint, guid, cancellationToken);
        }

        public Task<Guid?> TryPopNextRequestGuid(Uri endpoint, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.TryPopNextRequestGuid(endpoint, cancellationToken);
        }

        public virtual Task PutRequest(Uri endpoint, Guid requestId, string payload, TimeSpan requestPickupTimeout, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PutRequest(endpoint, requestId, payload, requestPickupTimeout, cancellationToken);
        }

        public Task<string?> TryGetAndRemoveRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.TryGetAndRemoveRequest(endpoint, requestId, cancellationToken);
        }

        public Task<bool> IsRequestStillOnQueue(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.IsRequestStillOnQueue(endpoint, requestId, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToRequestCancellation(Uri endpoint, Guid request, Func<Task> onCancellationReceived, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToRequestCancellation(endpoint, request, onCancellationReceived, cancellationToken);
        }

        public Task PublishCancellation(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PublishCancellation(endpoint, requestId, cancellationToken);
        }

        public string RequestCancelledMarkerKey(Uri endpoint, Guid requestId)
        {
            return halibutRedisTransport.RequestCancelledMarkerKey(endpoint, requestId);
        }

        public Task MarkRequestAsCancelled(Uri endpoint, Guid requestId, TimeSpan ttl, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.MarkRequestAsCancelled(endpoint, requestId, ttl, cancellationToken);
        }

        public Task<bool> IsRequestMarkedAsCancelled(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.IsRequestMarkedAsCancelled(endpoint, requestId, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToNodeHeartBeatChannel(Uri endpoint, Guid request, HalibutQueueNodeSendingPulses nodeSendingPulsesType, Func<Task> onHeartBeat, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToNodeHeartBeatChannel(endpoint, request, nodeSendingPulsesType, onHeartBeat, cancellationToken);
        }

        public Task SendHeartBeatFromNodeProcessingTheRequest(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SendHeartBeatFromNodeProcessingTheRequest(endpoint, requestId, nodeSendingPulsesType, cancellationToken);
        }

        public Task SendHeartBeatFromNodeProcessingTheRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SendHeartBeatFromNodeProcessingTheRequest(endpoint, requestId, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToNodeProcessingTheRequestHeartBeatChannel(Uri endpoint, Guid request, Func<Task> onHeartBeat, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToNodeProcessingTheRequestHeartBeatChannel(endpoint, request, onHeartBeat, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToResponseChannel(Uri endpoint, Guid identifier, Func<string, Task> onValueReceived, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToResponseChannel(endpoint, identifier, onValueReceived, cancellationToken);
        }

        public Task PublishThatResponseIsAvailable(Uri endpoint, Guid identifier, string value, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PublishThatResponseIsAvailable(endpoint, identifier, value, cancellationToken);
        }

        public Task MarkThatResponseIsSet(Uri endpoint, Guid identifier, string value, TimeSpan ttl, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.MarkThatResponseIsSet(endpoint, identifier, value, ttl, cancellationToken);
        }

        public Task<string?> GetResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.GetResponseMessage(endpoint, identifier, cancellationToken);
        }

        public Task<bool> DeleteResponse(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.DeleteResponse(endpoint, identifier, cancellationToken);
        }
    }
}