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
using Halibut.Diagnostics;
using Halibut.Util;
using StackExchange.Redis;

namespace Halibut.Queue.Redis
{
    public class ResilientSubscriber : IAsyncDisposable
    {
        readonly ConnectionMultiplexer Connection;
        readonly string channelName;
        readonly Func<ChannelMessage, Task> onMessage;
        readonly Func<RedisFacade.ConnectionInErrorHelper> ConnectionInErrorProvider;
        readonly ILog log;

        CancellationTokenSource staySubscribedCancellationTokenSource;

        public ResilientSubscriber(ConnectionMultiplexer connection, 
            string channelName, 
            Func<ChannelMessage, Task> onMessage,
            CancellationToken cancellationTokenForConnectionMultiplexer,
            Func<RedisFacade.ConnectionInErrorHelper> connectionInErrorProvider, ILog log)
        {
            Connection = connection;
            this.channelName = channelName;
            this.onMessage = onMessage;
            ConnectionInErrorProvider = connectionInErrorProvider;
            this.log = log;
            this.staySubscribedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenForConnectionMultiplexer);
        }

        public async Task StartSubscribe()
        {
            log?.Write(EventType.Diagnostic, $"Starting resilient subscription to channel: {channelName}");
            await Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                await Try.IgnoringError(async () => await KeepResubscribingShouldConnectionFail());
            });
        }
        async Task KeepResubscribingShouldConnectionFail()
        {
            var staySubscribedCancellationToken = staySubscribedCancellationTokenSource.Token;
            while (!staySubscribedCancellationToken.IsCancellationRequested)
            {
                try
                {
                    var (connectionInError, channelMessageQueue) = await subscribeToChannel(staySubscribedCancellationToken);
                    await using var _ = new FuncAsyncDisposable(() => Try.IgnoringError(() => channelMessageQueue.UnsubscribeAsync()));
                    if(staySubscribedCancellationTokenSource.IsCancellationRequested) return;
                    log?.Write(EventType.Diagnostic, $"Waiting for connection error on channel: {channelName}");
                    
                    // Now wait for a connection error to occur since we started to subscribe.
                    await connectionInError.CompletesWhenAConnectionErrorOccurs.WaitAsync(staySubscribedCancellationToken);
                    log?.Write(EventType.Diagnostic, $"Connection error detected on channel: {channelName}, resubscribing");
                }
                catch (Exception ex)
                {
                    log?.Write(EventType.Error, $"Error in subscription loop for channel {channelName}: {ex.Message}");
                }
            }
        }

        async Task<(RedisFacade.ConnectionInErrorHelper connectionInError, ChannelMessageQueue channelMessageQueue)> 
            subscribeToChannel(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var connectionInError = ConnectionInErrorProvider();
                    
                    if (!connectionInError.IsConnectionInError && Connection.GetSubscriber().IsConnected())
                    {
                        log?.Write(EventType.Diagnostic, $"Successfully subscribing to channel: {channelName}");
                        var channelMessageQueue = await Connection.GetSubscriber()
                            .SubscribeAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Literal));
                        channelMessageQueue.OnMessage(onMessage);
                        log?.Write(EventType.Diagnostic, $"Successfully subscribed to channel: {channelName}");
                        return (connectionInError, channelMessageQueue);
                    }
                    else
                    {
                        log?.Write(EventType.Diagnostic, $"Connection not ready for channel {channelName}, waiting 5 seconds before retry");
                        await Task.Delay(5000, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    log?.Write(EventType.Error, $"Error subscribing to channel {channelName}: {ex.Message}, retrying in 5 seconds");
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            log?.Write(EventType.Diagnostic, $"Disposing resilient subscriber for channel: {channelName}");
            await Try.IgnoringError(async () => await staySubscribedCancellationTokenSource.CancelAsync());
            Try.IgnoringError(() => staySubscribedCancellationTokenSource.Dispose());
        }
    }
}