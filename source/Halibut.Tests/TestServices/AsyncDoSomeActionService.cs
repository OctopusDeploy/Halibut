using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public class AsyncDoSomeActionService : IAsyncDoSomeActionService
    {
        public Action ActionDelegate { get; set; }

        public AsyncDoSomeActionService() : this(() => { })
        {
            
        }
        public AsyncDoSomeActionService(Action action)
        {
            this.ActionDelegate = action;
        }

        public async Task ActionAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            ActionDelegate();
        }
    }
}