using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class PollingClient : IPollingClient
    {
        readonly Uri subscription;
        readonly ISecureClient secureClient;
        readonly Func<RequestMessage, ResponseMessage> handleIncomingRequest;
        readonly ILog log;
        readonly Thread thread;
        readonly ConcurrentDictionary<Thread, MessageExchangeProtocol> runningExchanges = new ConcurrentDictionary<Thread, MessageExchangeProtocol>();
        bool working;
        int longRunningTransferCount;

        [Obsolete("Use the overload that provides a logger. This remains for backwards compatibility.")]
        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest)
            : this(subscription, secureClient, handleIncomingRequest, null)
        {
        }

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, ILog log)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
            this.log = log;
            thread = new Thread(ExecutePollingLoop);
            thread.Name = "Polling loop for " + secureClient.ServiceEndpoint + " for subscription " + subscription;
            thread.IsBackground = true;
        }

        public void Start()
        {
            working = true;
            thread.Start();
        }

        private void ExecutePollingLoop(object ignored)
        {
            var timeSinceLastRampUp = new Stopwatch();
            while (working)
            {
                var completed = runningExchanges.Keys.Where(t => !t.IsAlive).ToArray();
                foreach (var item in completed)
                    runningExchanges.TryRemove(item, out _);

                Console.WriteLine(runningExchanges.Count);

                var longRunning = Interlocked.Exchange(ref longRunningTransferCount, 0);

                if (longRunning > 0 || runningExchanges.Count == 0)
                {
                    StartNewExchangeThread();
                    timeSinceLastRampUp.Restart();
                }
                
                if(timeSinceLastRampUp.Elapsed > TimeSpan.FromMinutes(2))
                {
                    // Slowly ramp down the number of connections
                    var protocol = runningExchanges.Values.Where(p => p != null && !p.StopExchangeAsSubscriberOnceNoMessagesAreLeft).Skip(1).FirstOrDefault();
                    if (protocol != null)
                        protocol.StopExchangeAsSubscriberOnceNoMessagesAreLeft = true;
                }

                Thread.Sleep(5000);
            }
        }

        void StartNewExchangeThread()
        {
            Thread exchangeThread = null;

            exchangeThread = new Thread(() => Run(p => runningExchanges[exchangeThread] = p))
            {
                Name = "Polling client for " + secureClient.ServiceEndpoint + " for subscription " + subscription,
                IsBackground = true
            };
            runningExchanges[exchangeThread] = null;
            exchangeThread.Start();
        }

        void Run(Action<MessageExchangeProtocol> gotProtocolCallback)
        {
            try
            {
                secureClient.ExecuteTransaction(protocol =>
                {
                    gotProtocolCallback(protocol);
                    return protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest, () => Interlocked.Increment(ref longRunningTransferCount));
                });
            }
            catch (Exception ex)
            {
                log?.WriteException(EventType.Error, "Exception in the polling loop, sleeping for 5 seconds. This may be cause by a network error and usually rectifies itself. Disregard this message unless you are having communication problems.", ex);
            }
        }

        public void Dispose()
        {
            working = false;
        }
    }
}