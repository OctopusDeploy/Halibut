using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices
{
    public class AsyncComplexObjectService : IAsyncComplexObjectService
    {
        public async Task<ComplexObjectMultipleDataStreams> ProcessAsync(ComplexObjectMultipleDataStreams request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new ComplexObjectMultipleDataStreams
            {
                Payload1 = await ReadIntoNewDataStream(request.Payload1, cancellationToken),
                Payload2 = await ReadIntoNewDataStream(request.Payload2, cancellationToken)
            };
        }

        public async Task<ComplexObjectMultipleChildren> ProcessAsync(ComplexObjectMultipleChildren request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new ComplexObjectMultipleChildren
            {
                Child1 = new ComplexChild1
                {
                    ChildPayload1 = await ReadIntoNewDataStream(request.Child1!.ChildPayload1, cancellationToken),
                    ChildPayload2 = await ReadIntoNewDataStream(request.Child1!.ChildPayload2, cancellationToken),
                    ListOfStreams = await request.Child1.ListOfStreams!.ToAsyncEnumerable().Do(ReadIntoNewDataStream).ToListAsync(cancellationToken),
                    DictionaryPayload = request.Child1.DictionaryPayload!.ToDictionary(pair => pair.Key, pair => pair.Value),
                },
                Child2 = new ComplexChild2
                {
                    EnumPayload = request.Child2!.EnumPayload,
                    ComplexPayloadSet = await request.Child2.ComplexPayloadSet!.ToAsyncEnumerable().Do(async x => new ComplexPair<DataStream>(x.EnumValue, await ReadIntoNewDataStream(x.Payload, cancellationToken))).ToHashSetAsync(cancellationToken)
                }
            };
        }

        public async Task<ComplexObjectWithInheritance> ProcessAsync(ComplexObjectWithInheritance request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new ComplexObjectWithInheritance
            {
                Child1 = new ComplexInheritedChild1(request.Child1!.Name),
                Child2 = new ComplexInheritedChild2(request.Child2!.Description)
            };
        }

        async Task<DataStream> ReadIntoNewDataStream(DataStream? ds, CancellationToken cancellationToken)
        {
            // Read the source DataStream, then write into
            // a new DataStream, to simulate some work.
            // i.e. we don't want to just re-use the exact same
            // DataStream instance
            return DataStream.FromString(await ds!.ReadAsString(cancellationToken));
        }
    }
}