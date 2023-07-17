using System;
using System.Collections.Generic;

namespace Halibut.TestUtils.Contracts
{
    public interface IComplexObjectService
    {
        ComplexResponse Process(ComplexRequest request);
    }

    public class ComplexRequest
    {
        public string RequestId;
        public DataStream Payload;
        public int NumberOfCopies;

        public ComplexObjectChild Child;
    }

    public class ComplexResponse
    {
        public string RequestId;
        public IDictionary<Guid, IList<DataStream>> Payloads;

        public ComplexObjectChild Child;
    }

    public class ComplexObjectChild
    {
        public DataStream First;
        public DataStream Second;
    }

    public class ComplexObjectService : IComplexObjectService
    {
        public ComplexResponse Process(ComplexRequest request)
        {
            IList<DataStream> dataStreams = new List<DataStream>();
            string payload = request.Payload.ReadAsString();
            for (int i = 1; i <= request.NumberOfCopies; i++)
            {
                dataStreams.Add(DataStream.FromString(i + ": " + payload));
            }

            return new ComplexResponse
            {
                RequestId = request.RequestId,
                Payloads = new Dictionary<Guid, IList<DataStream>>
                {
                    {Guid.NewGuid(), dataStreams}
                },
                Child = new ComplexObjectChild
                {
                    First = DataStream.FromString("First: " + request.Child.First.ReadAsString()),
                    Second = DataStream.FromString("Second: " + request.Child.Second.ReadAsString()),
                }
            };
        }
    }
}
