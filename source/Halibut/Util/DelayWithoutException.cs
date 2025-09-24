using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{
    public static class DelayWithoutException
    {
        public static Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            return Task.Delay(timeSpan, cancellationToken).ContinueWith(t => { }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}