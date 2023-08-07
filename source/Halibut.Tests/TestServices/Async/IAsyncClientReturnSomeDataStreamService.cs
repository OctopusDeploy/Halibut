using System;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientReturnSomeDataStreamService
    {
        public Task<DataStream> SomeDataStreamAsync();
    }
}