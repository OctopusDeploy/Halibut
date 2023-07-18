using System;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.TestUtils.Contracts
{
    public interface IComplexObjectService
    {
        ComplexResponse Process(ComplexRequest request);
    }

    public class ComplexRequest
    {
        public string RequestId;

        public DataStream Payload1;
        public DataStream Payload2;

        public ComplexChild Child1;
        public ComplexChild Child2;
    }

    public class ComplexResponse
    {
        public string RequestId;

        public DataStream Payload1;
        public DataStream Payload2;

        public ComplexChild Child1;
        public ComplexChild Child2;
    }

    public class ComplexChild
    {
        public DataStream ChildPayload1;
        public DataStream ChildPayload2;

        public IList<DataStream> ListOfStreams;
        public IDictionary<Guid, string> DictionaryPayload;
        public ComplexEnum EnumPayload;
        public ISet<ComplexPair<DataStream>> ComplexPayloadSet;
    }

    public enum ComplexEnum
    {
        RequestValue1,
        RequestValue2,
        RequestValue3
    }

    public class ComplexPair<T>: IEquatable<ComplexPair<T>>
    {
        public ComplexPair(ComplexEnum enumValue, T payload)
        {
            EnumValue = enumValue;
            Payload = payload;
        }

        public readonly ComplexEnum EnumValue;
        public readonly T Payload;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ComplexPair<T>)obj);
        }

        public bool Equals(ComplexPair<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EnumValue.Equals(other.EnumValue) && Payload.Equals(other.Payload);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<ComplexEnum>.Default.GetHashCode(EnumValue) * 397) ^ EqualityComparer<T>.Default.GetHashCode(Payload);
            }
        }
    }

    public class ComplexObjectService : IComplexObjectService
    {
        public ComplexResponse Process(ComplexRequest request)
        {
            return new ComplexResponse
            {
                RequestId = request.RequestId,
                Payload1 = ReadIntoNewDataStream(request.Payload1),
                Payload2 = ReadIntoNewDataStream(request.Payload2),
                Child1 = MapToResponseChild(request.Child1),
                Child2 = MapToResponseChild(request.Child2)
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

        ComplexChild MapToResponseChild(ComplexChild child)
        {
            return new ComplexChild
            {
                ChildPayload1 = ReadIntoNewDataStream(child.ChildPayload1),
                ChildPayload2 = ReadIntoNewDataStream(child.ChildPayload2),
                ListOfStreams = child.ListOfStreams.Select(ReadIntoNewDataStream).ToList(),
                EnumPayload = child.EnumPayload,
                DictionaryPayload = child.DictionaryPayload.ToDictionary(pair => pair.Key, pair => pair.Value),
                ComplexPayloadSet = child.ComplexPayloadSet.Select(x => new ComplexPair<DataStream>(x.EnumValue, ReadIntoNewDataStream(x.Payload))).ToHashSet()
            };
        }
    }
}
