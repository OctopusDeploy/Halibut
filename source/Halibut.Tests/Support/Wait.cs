using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.TestUtils.Contracts;
using Serilog;

namespace Halibut.Tests.Support
{
    public static class Wait
    {
        public static async Task For(Func<Task<bool>> toBecomeTrue, CancellationToken cancellationToken)
        {
            while (!await toBecomeTrue())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }
        }
        
        public static void UntilActionSucceeds(Action actionToNotThrow,
            TimeSpan timeToWait,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(new CancellationTokenSource(timeToWait).Token, cancellationToken);

            var sw = Stopwatch.StartNew();
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    actionToNotThrow();
                    return;
                }
                catch (Exception e)
                {
                    Thread.Sleep(20);
                    if (cts.IsCancellationRequested)
                    {
                        throw;
                    }

                    if (sw.Elapsed > TimeSpan.FromSeconds(10))
                    {
                        sw.Reset();
                        logger.Information(e, "Still waiting for action to be a success");
                    }
                }
            }
            cts.Token.ThrowIfCancellationRequested();
            
        }
    }
}