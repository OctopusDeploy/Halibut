using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices
{
    public class AsyncListService : IAsyncListService
    {
        readonly List<string> data;

        public bool WasCalled { get; private set; }

        public AsyncListService(IEnumerable<string> data)
        {
            this.data = data.ToList();
        }
        public async Task<List<string>> GetListAsync(CancellationToken cancellationToken)
        {
            WasCalled = true;
            await Task.CompletedTask;
            return data;
        }
    }
}