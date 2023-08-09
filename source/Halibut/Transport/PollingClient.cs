#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Transport
{
    public class PollingClient : IPollingClient
    {
        readonly Func<RequestMessage, ResponseMessage> handleIncomingRequest;
        readonly ILog log;
        readonly ISecureClient secureClient;
        readonly Uri subscription;
        Thread? pollingClientLoopThread;
        bool working;

        Task? pollingClientLoopTask;
        readonly CancellationTokenSource workingCancellationTokenSource;
        readonly CancellationToken cancellationToken;
        
        readonly Func<RetryPolicy> createRetryPolicy;
        readonly AsyncHalibutFeature asyncHalibutFeature;
        

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, ILog log, CancellationToken cancellationToken, Func<RetryPolicy> createRetryPolicy, AsyncHalibutFeature asyncHalibutFeature)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
            this.log = log;
            this.cancellationToken = cancellationToken;
            workingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.createRetryPolicy = createRetryPolicy;
            this.asyncHalibutFeature = asyncHalibutFeature;
            
        }

        public void Start()
        {
            working = true;

            if (asyncHalibutFeature == AsyncHalibutFeature.Disabled)
            {
                pollingClientLoopThread = new Thread(ExecutePollingLoop!);
                pollingClientLoopThread.Name = "Polling client for " + secureClient.ServiceEndpoint + " for subscription " + subscription;
                pollingClientLoopThread.IsBackground = true;
            }
            else
            {
                pollingClientLoopTask = Task.Run(async () => await ExecutePollingLoopAsync());
            }
        }

        public void Dispose()
        {
            working = false;
            try
            {
                workingCancellationTokenSource.Cancel();
            }
            catch
            {
            }

            try
            {
                workingCancellationTokenSource.Dispose();
            }
            catch
            {
            }
        }
        
        void ExecutePollingLoop(object ignored)
        {
            var retry = createRetryPolicy();
            var sleepFor = TimeSpan.Zero;
            while (working)
            {
                try
                {
                    try
                    {
                        retry.Try();
#pragma warning disable CS0612 // Type or member is obsolete
                        secureClient.ExecuteTransaction(protocol =>
                        {
                            // We have successfully connected at this point so reset the retry policy
                            // Subsequent connection issues will try and reconnect quickly and then back-off
                            retry.Success();
                            protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest);
                        }, cancellationToken);
#pragma warning restore CS0612
                        retry.Success();
                    }
                    finally
                    {
                        sleepFor = retry.GetSleepPeriod();
                    }
                }
                catch (HalibutClientException ex)
                {
                    log?.WriteException(EventType.Error, $"Halibut client exception: {ex.Message?.TrimEnd('.')}. Retrying in {sleepFor.TotalSeconds:n1} seconds", ex);
                }
                catch (Exception ex)
                {
                    log?.WriteException(EventType.Error, $"Exception in the polling loop. Retrying in {sleepFor.TotalSeconds:n1} seconds. This may be cause by a network error and usually rectifies itself. Disregard this message unless you are having communication problems.", ex);
                }
                finally
                {
                    Thread.Sleep(sleepFor);
                }
            }
        }
        
        async Task ExecutePollingLoopAsync()
        {
            var retry = createRetryPolicy();
            var sleepFor = TimeSpan.Zero;
            while (!workingCancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    try
                    {
                        retry.Try();
                        await secureClient.ExecuteTransactionAsync(async (protocol, cancellationToken) =>
                        {
                            // We have successfully connected at this point so reset the retry policy
                            // Subsequent connection issues will try and reconnect quickly and then back-off
                            retry.Success();
                            await protocol.ExchangeAsSubscriberAsync(subscription, handleIncomingRequest, int.MaxValue, cancellationToken);
                        }, workingCancellationTokenSource.Token);
                        retry.Success();
                    }
                    finally
                    {
                        sleepFor = retry.GetSleepPeriod();
                    }
                }
                catch (HalibutClientException ex)
                {
                    log?.WriteException(EventType.Error, $"Halibut client exception: {ex.Message?.TrimEnd('.')}. Retrying in {sleepFor.TotalSeconds:n1} seconds", ex);
                }
                catch (Exception ex)
                {
                    log?.WriteException(EventType.Error, $"Exception in the polling loop. Retrying in {sleepFor.TotalSeconds:n1} seconds. This may be cause by a network error and usually rectifies itself. Disregard this message unless you are having communication problems.", ex);
                }
                finally
                {
                    await Task.Delay(sleepFor, workingCancellationTokenSource.Token);
                }
            }
        }
    }
}