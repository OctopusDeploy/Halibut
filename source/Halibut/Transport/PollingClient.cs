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
        Task? pollingClientLoopTask;
        readonly CancellationTokenSource cancellationTokenSource;
        readonly Func<RetryPolicy> createRetryPolicy;

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, ILog log, CancellationToken cancellationToken, Func<RetryPolicy> createRetryPolicy)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
            this.log = log;
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.createRetryPolicy = createRetryPolicy;
        }
        
        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, Task<ResponseMessage>> handleIncomingRequestAsync, ILog log, CancellationToken cancellationToken, Func<RetryPolicy> createRetryPolicy)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequestAsync = handleIncomingRequestAsync;
            this.log = log;
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.createRetryPolicy = createRetryPolicy;
        }

        public void Start()
        {
            var requestCancellationToken = cancellationTokenSource.Token;
            pollingClientLoopTask = Task.Run(async () => await ExecutePollingLoopAsyncCatchingExceptions(requestCancellationToken));
        }

        public void Dispose()
        {
            Try.CatchingError(cancellationTokenSource.Cancel, _ => { });
            Try.CatchingError(cancellationTokenSource.Dispose, _ => { });
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
                        }, cancellationToken);
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
