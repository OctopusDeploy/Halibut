using System.Threading.Tasks;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientComplexObjectService
    {
        Task<ComplexObjectMultipleDataStreams> ProcessAsync(ComplexObjectMultipleDataStreams request);
        Task<ComplexObjectMultipleChildren> ProcessAsync(ComplexObjectMultipleChildren request);
        Task<ComplexObjectWithInheritance> ProcessAsync(ComplexObjectWithInheritance request);
    }
}