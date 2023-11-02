using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices
{
    public interface IAsyncComplexObjectService
    {
        Task<ComplexObjectMultipleDataStreams> ProcessAsync(ComplexObjectMultipleDataStreams request, CancellationToken cancellationToken);
        Task<ComplexObjectMultipleChildren> ProcessAsync(ComplexObjectMultipleChildren request, CancellationToken cancellationToken);
        Task<ComplexObjectWithInheritance> ProcessAsync(ComplexObjectWithInheritance request, CancellationToken cancellationToken);
    }
}