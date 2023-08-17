using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public class BrokenConventionService : IBrokenConventionService
    {
        public string SayHello(string name)
        {
            return $"Hello {name}!";
        }

        public int GetRandomNumberMissingSuffix()
        {
            // https://xkcd.com/221
            return 4;
        }

        public int GetRandomNumberMissingCancellationToken()
        {
            // https://xkcd.com/221
            return 4;
        }

        public string SayHelloMissingSuffix(string name)
        {
            return $"Hello {name}!";
        }

        public string SayHelloMissingCancellationToken(string name)
        {
            return $"Hello {name}!";
        }
    }

    public class AsyncBrokenConventionService : IAsyncBrokenConventionService
    {
        IBrokenConventionService service = new BrokenConventionService();

        public async Task<int> GetRandomNumberMissingSuffix(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.GetRandomNumberMissingSuffix();
        }

        public async Task<int> GetRandomNumberMissingCancellationTokenAsync()
        {
            await Task.CompletedTask;
            return service.GetRandomNumberMissingCancellationToken();
        }

        public async Task<string> SayHelloMissingSuffix(string name, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.SayHelloMissingSuffix(name);
        }

        public async Task<string> SayHelloMissingCancellationTokenAsync(string name)
        {
            await Task.CompletedTask;
            return service.SayHelloMissingCancellationToken(name);
        }
    }
}
