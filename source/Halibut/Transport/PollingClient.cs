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
        readonly Func<RequestMessage, ResponseMessage>? handleIncomingRequest;
        readonly Func<RequestMessage, Task<ResponseMessage>>? handleIncomingRequestAsync;
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
        
        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, Task<ResponseMessage>> handleIncomingRequestAsync, ILog log, CancellationToken cancellationToken, Func<RetryPolicy> createRetryPolicy, AsyncHalibutFeature asyncHalibutFeature)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequestAsync = handleIncomingRequestAsync;
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
                pollingClientLoopThread.Start();
            }
            else
            {
                pollingClientLoopTask = Task.Run(async () => await ExecutePollingLoopAsyncCatchingExceptions(workingCancellationTokenSource.Token));
            }
        }

        public void Dispose()
        {
            working = false;
            Try.CatchingError(workingCancellationTokenSource.Cancel, _ => { });
            Try.CatchingError(workingCancellationTokenSource.Dispose, _ => { });
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

        /// <summary>
        /// Runs ExecutePollingLoopAsync but catches any exception that falls out of it, log here
        /// rather than let it be unobserved. We are not expecting an exception but just in case.
        /// </summary>
        async Task ExecutePollingLoopAsyncCatchingExceptions(CancellationToken cancellationToken)
        {
            try
            {
                await ExecutePollingLoopAsync(cancellationToken);
            }
            catch (Exception e)
            {
                log.Write(EventType.Diagnostic, $"PollingClient stopped with an exception: {e}");
            }
        }

        async Task ExecutePollingLoopAsync(CancellationToken cancellationToken)
        {
            using var requestCancellationTokens = new RequestCancellationTokens(cancellationToken, cancellationToken);
            var retry = createRetryPolicy();
            var sleepFor = TimeSpan.Zero;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    try
                    {
                        retry.Try();
                        await secureClient.ExecuteTransactionAsync(async (protocol, ct) =>
                        {
                            // We have successfully connected at this point so reset the retry policy
                            // Subsequent connection issues will try and reconnect quickly and then back-off
                            retry.Success();
                            await protocol.ExchangeAsSubscriberAsync(subscription, handleIncomingRequestAsync, int.MaxValue, ct);
                        }, requestCancellationTokens);
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
                    await Task.Delay(sleepFor, cancellationToken);
                }
            }
        }
    }
}
