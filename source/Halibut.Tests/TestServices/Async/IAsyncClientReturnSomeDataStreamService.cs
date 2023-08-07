using System;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientReturnSomeDataStreamService
    {
        Task<DataStream> SomeDataStreamAsync();
    }
}