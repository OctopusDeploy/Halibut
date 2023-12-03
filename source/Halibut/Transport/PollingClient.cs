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
        readonly Func<RequestMessage, Task<ResponseMessage>> handleIncomingRequestAsync;
        readonly ILog log;
        readonly ISecureClient secureClient;
        readonly Uri subscription;

        Task? pollingClientLoopTask;
        readonly CancellationTokenSource workingCancellationTokenSource;
        readonly CancellationToken cancellationToken;
        
        readonly Func<RetryPolicy> createRetryPolicy;
        RequestCancellationTokens? requestCancellationTokens;
        
        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, Task<ResponseMessage>> handleIncomingRequestAsync, ILog log, CancellationToken cancellationToken, Func<RetryPolicy> createRetryPolicy)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequestAsync = handleIncomingRequestAsync;
            this.log = log;
            this.cancellationToken = cancellationToken;
            workingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.createRetryPolicy = createRetryPolicy;
        }

        public void Start()
        {
            requestCancellationTokens = new RequestCancellationTokens(workingCancellationTokenSource.Token, workingCancellationTokenSource.Token);
            pollingClientLoopTask = Task.Run(async () => await ExecutePollingLoopAsyncCatchingExceptions(requestCancellationTokens));
        }

        public void Dispose()
        {
            Try.CatchingError(workingCancellationTokenSource.Cancel, _ => { });
            Try.CatchingError(workingCancellationTokenSource.Dispose, _ => { });
            Try.CatchingError(() => requestCancellationTokens?.Dispose(), _ => { });
        }

        /// <summary>
        /// Runs ExecutePollingLoopAsync but catches any exception that falls out of it, log here
        /// rather than let it be unobserved. We are not expecting an exception but just in case.
        /// </summary>
        async Task ExecutePollingLoopAsyncCatchingExceptions(RequestCancellationTokens requestCancellationTokens)
        {
            try
            {
                await ExecutePollingLoopAsync(requestCancellationTokens);
            }
            catch (Exception e)
            {
                log.Write(EventType.Diagnostic, $"PollingClient stopped with an exception: {e}");
            }
        }

        async Task ExecutePollingLoopAsync(RequestCancellationTokens requestCancellationTokens)
        {
            var retry = createRetryPolicy();
            var sleepFor = TimeSpan.Zero;
            while (!requestCancellationTokens.LinkedCancellationToken.IsCancellationRequested)
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
                    await Task.Delay(sleepFor, requestCancellationTokens.LinkedCancellationToken);
                }
            }
        }
    }
}
