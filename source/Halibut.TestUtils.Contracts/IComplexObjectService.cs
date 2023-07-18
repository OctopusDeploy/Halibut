using System;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.TestUtils.Contracts
{
    public interface IComplexObjectService
    {
        ComplexObjectMultipleDataStreams Process(ComplexObjectMultipleDataStreams request);
        ComplexObjectMultipleChildren Process(ComplexObjectMultipleChildren request);
    }

    public class ComplexObjectMultipleDataStreams
    {
        public DataStream Payload1;
        public DataStream Payload2;
    }

    public class ComplexObjectMultipleChildren
    {
        public ComplexChild1 Child1;
        public ComplexChild2 Child2;
    }

    public class ComplexChild1
    {
        public DataStream ChildPayload1;
        public DataStream ChildPayload2;
        public IList<DataStream> ListOfStreams;
        public IDictionary<Guid, string> DictionaryPayload;
    }

    public class ComplexChild2
    {
        public ComplexEnum EnumPayload;
        public ISet<ComplexPair<DataStream>> ComplexPayloadSet;
    }
    
    public enum ComplexEnum
    {
        RequestValue1,
        RequestValue2,
        RequestValue3
    }

    public class ComplexPair<T> : IEquatable<ComplexPair<T>>
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
        public ComplexObjectMultipleDataStreams Process(ComplexObjectMultipleDataStreams request)
        {
            return new ComplexObjectMultipleDataStreams
            {
                Payload1 = ReadIntoNewDataStream(request.Payload1),
                Payload2 = ReadIntoNewDataStream(request.Payload2)
            };
        }

        public ComplexObjectMultipleChildren Process(ComplexObjectMultipleChildren request)
        {
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
