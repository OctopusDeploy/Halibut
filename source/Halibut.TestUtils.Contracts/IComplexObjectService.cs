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

        public ComplexRequestChild Child1;
        public ComplexRequestChild Child2;
    }

    public class ComplexResponse
    {
        public string RequestId;

        public DataStream Payload1;
        public DataStream Payload2;

        public ComplexResponseChild Child1;
        public ComplexResponseChild Child2;
    }

    public abstract class ComplexChildBase<T>
    {
        public DataStream ChildPayload1;
        public DataStream ChildPayload2;

        public IList<DataStream> ListOfStreams;
        public IDictionary<Guid, string> DictionaryPayload;
        public T EnumPayload;
        public ISet<ComplexPair<T, DataStream>> ComplexPayloadSet;
    }

    public class ComplexRequestChild : ComplexChildBase<ComplexRequestEnum>
    {
    }

    public class ComplexResponseChild : ComplexChildBase<ComplexResponseEnum>
    {
    }

    public enum ComplexRequestEnum
    {
        RequestValue1,
        RequestValue2,
        RequestValue3
    }

    public class ComplexPair<T1, T2>: IEquatable<ComplexPair<T1, T2>>
    {
        public ComplexPair(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public readonly T1 Item1;
        public readonly T2 Item2;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ComplexPair<T1, T2>)obj);
        }

        public bool Equals(ComplexPair<T1, T2> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Item1.Equals(other.Item1) && Item2.Equals(other.Item2);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T1>.Default.GetHashCode(Item1) * 397) ^ EqualityComparer<T2>.Default.GetHashCode(Item2);
            }
        }
    }

    public enum ComplexResponseEnum
    {
        ResponseValue1,
        ResponseValue2,
        ResponseValue3
    }

    public class ComplexObjectService : IComplexObjectService
    {
        public ComplexResponse Process(ComplexRequest request)
        {
            return new ComplexResponse
            {
                RequestId = request.RequestId,
                Payload1 = AddResponsePrefix(request.Payload1),
                Payload2 = AddResponsePrefix(request.Payload2),
                Child1 = MapToResponseChild(request.Child1),
                Child2 = MapToResponseChild(request.Child2)
            };
        }

        DataStream AddResponsePrefix(DataStream ds)
        {
            return DataStream.FromString(AddResponsePrefix(ds.ReadAsString()));
        }

        string AddResponsePrefix(string s)
        {
            return "Response: " + s;
        }

        ComplexResponseChild MapToResponseChild(ComplexRequestChild requestChild)
        {
            return new ComplexResponseChild
            {
                ChildPayload1 = AddResponsePrefix(requestChild.ChildPayload1),
                ChildPayload2 = AddResponsePrefix(requestChild.ChildPayload2),
                ListOfStreams = requestChild.ListOfStreams.Select(AddResponsePrefix).ToList(),
                EnumPayload = EnumMap[requestChild.EnumPayload],
                DictionaryPayload = requestChild.DictionaryPayload.ToDictionary(pair => pair.Key, pair => AddResponsePrefix(pair.Value)),
                ComplexPayloadSet = requestChild.ComplexPayloadSet.Select(x => new ComplexPair<ComplexResponseEnum, DataStream>(EnumMap[x.Item1], AddResponsePrefix(x.Item2))).ToHashSet()
            };
        }

        static readonly Dictionary<ComplexRequestEnum, ComplexResponseEnum> EnumMap = new()
        {
            { ComplexRequestEnum.RequestValue1, ComplexResponseEnum.ResponseValue1 },
            { ComplexRequestEnum.RequestValue2, ComplexResponseEnum.ResponseValue2 },
            { ComplexRequestEnum.RequestValue3, ComplexResponseEnum.ResponseValue3 }
        };
    }
}
