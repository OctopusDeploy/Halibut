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
        public IList<DataStream> Payloads;

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
                dataStreams.Add(ProcessPayload(i, payload));
            }

            return new ComplexResponse
            {
                RequestId = request.RequestId,
                Payloads = dataStreams,
                Child = new ComplexObjectChild
                {
                    First = ProcessFirst(request.Child.First),
                    Second = ProcessSecond(request.Child.Second),
                }
                
            };
        }

        public static DataStream ProcessPayload(int copyNumber, string payload)
        {
            return DataStream.FromString(copyNumber + ": " + payload);
        }

        public static DataStream ProcessFirst(DataStream payload)
        {
            return DataStream.FromString("First: " + payload.ReadAsString());
        }

        public static DataStream ProcessSecond(DataStream payload)
        {
            return DataStream.FromString("Second: " + payload.ReadAsString());
        }
    }
}
