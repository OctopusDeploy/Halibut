using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Halibut.TestUtils.Contracts
{
    public interface IComplexObjectService
    {
        ComplexObjectMultipleDataStreams Process(ComplexObjectMultipleDataStreams request);
        ComplexObjectMultipleChildren Process(ComplexObjectMultipleChildren request);
        ComplexObjectWithInheritance Process(ComplexObjectWithInheritance request);
    }

    public class ComplexObjectMultipleDataStreams
    {
        public DataStream? Payload1;
        public DataStream? Payload2;
    }

    public class ComplexObjectMultipleChildren
    {
        public ComplexChild1? Child1;
        public ComplexChild2? Child2;
    }

    public class ComplexObjectWithInheritance
    {
        public IComplexChild? Child1 { get; set; }
        public ComplexChildBase? Child2 { get; set; }
    }

    public class ComplexChild1
    {
        public DataStream? ChildPayload1;
        public DataStream? ChildPayload2;
        public IList<DataStream>? ListOfStreams;
        public IDictionary<Guid, string>? DictionaryPayload;
    }

    public class ComplexChild2
    {
        public ComplexEnum EnumPayload;
        public ISet<ComplexPair<DataStream>>? ComplexPayloadSet;
    }

    public interface IComplexChild
    {
        string Name { get; }
    }

    public abstract class ComplexChildBase
    {
        public abstract string Description { get; }
    }

    public class ComplexInheritedChild1 : IComplexChild
    {
        [JsonConstructor]
        public ComplexInheritedChild1(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public class ComplexInheritedChild2 : ComplexChildBase
    {
        [JsonConstructor]
        public ComplexInheritedChild2(string description)
        {
            Description = description;
        }

        public override string Description { get; }
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

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ComplexPair<T>)obj);
        }

        public bool Equals(ComplexPair<T>? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EnumValue.Equals(other.EnumValue) && Payload is not null && Payload.Equals(other.Payload);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<ComplexEnum>.Default.GetHashCode(EnumValue) * 397) ^ EqualityComparer<T>.Default.GetHashCode(Payload!);
            }
        }
    }
}
