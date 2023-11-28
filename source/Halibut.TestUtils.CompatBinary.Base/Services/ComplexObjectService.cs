using System;
using System.Linq;
using Halibut.TestUtils.Contracts;

namespace Halibut.TestUtils.SampleProgram.Base.Services
{
    public class ComplexObjectService : IComplexObjectService
    {
        public ComplexObjectMultipleDataStreams Process(ComplexObjectMultipleDataStreams request)
        {
            return new ComplexObjectMultipleDataStreams
            {
                Payload1 = ReadIntoNewDataStream(request.Payload1!),
                Payload2 = ReadIntoNewDataStream(request.Payload2!)
            };
        }

        public ComplexObjectMultipleChildren Process(ComplexObjectMultipleChildren request)
        {
            return new ComplexObjectMultipleChildren
            {
                Child1 = new ComplexChild1
                {
                    ChildPayload1 = ReadIntoNewDataStream(request.Child1!.ChildPayload1!),
                    ChildPayload2 = ReadIntoNewDataStream(request.Child1!.ChildPayload2!),
                    ListOfStreams = request.Child1.ListOfStreams!.Select(ReadIntoNewDataStream).ToList(),
                    DictionaryPayload = request.Child1.DictionaryPayload!.ToDictionary(pair => pair.Key, pair => pair.Value),
                },
                Child2 = new ComplexChild2
                {
                    EnumPayload = request.Child2!.EnumPayload,
                    ComplexPayloadSet = request.Child2.ComplexPayloadSet!.Select(x => new ComplexPair<DataStream>(x.EnumValue, ReadIntoNewDataStream(x.Payload))).ToHashSet()
                }
            };
        }

        public ComplexObjectWithInheritance Process(ComplexObjectWithInheritance request)
        {
            return new ComplexObjectWithInheritance
            {
                Child1 = new ComplexInheritedChild1(request.Child1!.Name),
                Child2 = new ComplexInheritedChild2(request.Child2!.Description)
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