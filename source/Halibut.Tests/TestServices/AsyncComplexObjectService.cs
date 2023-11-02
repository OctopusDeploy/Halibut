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
                Payload1 = ReadIntoNewDataStream(request.Payload1),
                Payload2 = ReadIntoNewDataStream(request.Payload2)
            };
        }

        public async Task<ComplexObjectMultipleChildren> ProcessAsync(ComplexObjectMultipleChildren request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new ComplexObjectMultipleChildren
            {
                Child1 = new ComplexChild1
                {
                    ChildPayload1 = ReadIntoNewDataStream(request.Child1.ChildPayload1),
                    ChildPayload2 = ReadIntoNewDataStream(request.Child1.ChildPayload2),
                    ListOfStreams = request.Child1.ListOfStreams.Select(ReadIntoNewDataStream).ToList(),
                    DictionaryPayload = request.Child1.DictionaryPayload.ToDictionary(pair => pair.Key, pair => pair.Value),
                },
                Child2 = new ComplexChild2
                {
                    EnumPayload = request.Child2.EnumPayload,
                    ComplexPayloadSet = request.Child2.ComplexPayloadSet.Select(x => new ComplexPair<DataStream>(x.EnumValue, ReadIntoNewDataStream(x.Payload))).ToHashSet()
                }
            };
        }

        public async Task<ComplexObjectWithInheritance> ProcessAsync(ComplexObjectWithInheritance request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new ComplexObjectWithInheritance
            {
                Child1 = new ComplexInheritedChild1(request.Child1.Name),
                Child2 = new ComplexInheritedChild2(request.Child2.Description)
            };
        }

        DataStream ReadIntoNewDataStream(DataStream ds)
        {
            // Read the source DataStream, then write into
            // a new DataStream, to simulate some work.
            // i.e. we don't want to just re-use the exact same
            // DataStream instance
            return DataStream.FromString(ds.ReadAsString());
        }
    }
}