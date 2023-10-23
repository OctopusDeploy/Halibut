using System.Collections;
using System.Collections.Generic;
using FluentAssertions;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class TypeRegistryFixture
    {
        [Test]
        public void RegisteringAbstractTypeSearchesAssemblyForExportedDerivedTypes()
        {
            // Arrange
            var registry = new TypeRegistry();

            // Act
            registry.RegisterType(typeof(AbstractType), "$", false);

            // Assert
            registry.IsInAllowedTypes(typeof(AbstractType)).Should().BeTrue();
            registry.IsInAllowedTypes(typeof(IInterfaceType)).Should().BeFalse();
            registry.IsInAllowedTypes(typeof(PublicConcreteType)).Should().BeTrue();
            registry.IsInAllowedTypes(typeof(InternalConcreteType)).Should().BeFalse();
        }

        [Test]
        public void RegisteringInterfaceTypeSearchesAssemblyForExportedDerivedTypes()
        {
            // Arrange
            var registry = new TypeRegistry();

            // Act
            registry.RegisterType(typeof(IInterfaceType), "$", false);

            // Assert
            registry.IsInAllowedTypes(typeof(AbstractType)).Should().BeFalse();
            registry.IsInAllowedTypes(typeof(IInterfaceType)).Should().BeTrue();
            registry.IsInAllowedTypes(typeof(PublicConcreteType)).Should().BeTrue();
            registry.IsInAllowedTypes(typeof(InternalConcreteType)).Should().BeFalse();
        }

        [Test]
        public void RegisteringTypeSearchesAssemblyForExportedDerivedTypesAndShouldNotSearchEntireAppDomain()
        {
            // Arrange
            var registry = new TypeRegistry();

            // Act
            registry.RegisterType(typeof(IDictionary<string, string>), "$", false);

            // Assert
            registry.IsInAllowedTypes(typeof(IDictionary<string, string>)).Should().BeTrue();
            registry.IsInAllowedTypes(typeof(CustomDictionary)).Should().BeFalse(because: "The concrete implementation is in a different assembly to the IDictionary<string,string> type.");
        }

        [Test]
        public void RegisteringTypeSearchesSuppliedAssembliesForExportedDerivedTypes()
        {
            // Arrange
            var registry = new TypeRegistry(new[] { typeof(CustomDictionary).Assembly });

            // Act
            registry.RegisterType(typeof(IDictionary<string, string>), "$", false);

            // Assert
            registry.IsInAllowedTypes(typeof(IDictionary<string, string>)).Should().BeTrue();
            registry.IsInAllowedTypes(typeof(CustomDictionary)).Should().BeTrue();
        }
    }

    public abstract class AbstractType
    {
    }

    public interface IInterfaceType
    {
    }

    public class PublicConcreteType : AbstractType, IInterfaceType
    {
    }

    class InternalConcreteType : AbstractType, IInterfaceType
    {
    }

    public class CustomDictionary : IDictionary<string, string>
    {
        readonly IDictionary<string, string> _dictionaryImplementation;

        public CustomDictionary(IDictionary<string, string> dictionaryImplementation)
        {
            _dictionaryImplementation = dictionaryImplementation;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _dictionaryImplementation.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_dictionaryImplementation).GetEnumerator();
        }

        public void Add(KeyValuePair<string, string> item)
        {
            _dictionaryImplementation.Add(item);
        }

        public void Clear()
        {
            _dictionaryImplementation.Clear();
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            return _dictionaryImplementation.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            _dictionaryImplementation.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return _dictionaryImplementation.Remove(item);
        }

        public int Count => _dictionaryImplementation.Count;

        public bool IsReadOnly => _dictionaryImplementation.IsReadOnly;

        public void Add(string key, string value)
        {
            _dictionaryImplementation.Add(key, value);
        }

        public bool ContainsKey(string key)
        {
            return _dictionaryImplementation.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return _dictionaryImplementation.Remove(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return _dictionaryImplementation.TryGetValue(key, out value);
        }

        public string this[string key]
        {
            get => _dictionaryImplementation[key];
            set => _dictionaryImplementation[key] = value;
        }

        public ICollection<string> Keys => _dictionaryImplementation.Keys;

        public ICollection<string> Values => _dictionaryImplementation.Values;
    }
}