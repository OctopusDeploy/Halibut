using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Support
{
    public static class Try
    {
        public static void CatchingError(Action tryThisAction, Action<Exception> onFailure) 
        {
            try
            {
                tryThisAction();
            }
            catch (Exception e)
            {
                onFailure(e);
            }        
        }
        
        public static async Task DisposingAsync(IAsyncDisposable? toDispose, Action<Exception> onFailure)
        {
            try
            {
                if (toDispose is null)
                {
                    return;
                }

                await toDispose.DisposeAsync();
            }
            catch (Exception e)
            {
                onFailure(e);
            }

            
        }

        public static async Task<Exception?> CatchingError(Func<Task> tryThisAction)
        {
            try
            {
                await tryThisAction();
            }
            catch (Exception e)
            {
                return e;
            }

            return null;
        }

        public static async Task<Exception?> RunTillExceptionOrCancellation(Func<Task> action, CancellationToken cancellationToken)
        {
            Exception? actualException = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                actualException = await Try.CatchingError(async () => await action());

                if (actualException != null)
                {
                    break;
                }
            }

            return actualException;
        }
    }
}