using System;
using System.Threading;
using System.Threading.Tasks;

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
    }
}