using System;
using System.Threading.Tasks;

namespace Halibut.Util
{
    public class FuncAsyncDisposable : IAsyncDisposable
    {
        readonly Func<Task> disposer;

        public FuncAsyncDisposable(Func<Task> disposer)
        {
            this.disposer = disposer;
        }

        public async ValueTask DisposeAsync()
        {
            await this.disposer();
        }
    }
}