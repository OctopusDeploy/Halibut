using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class RegisteredSerializationBinderFixture
    {
        [Test]
        public void BindMethods_WithValidClass_FindsAllMethodTypes()
        {
            var binder = new RegisteredSerializationBinder(new[] { typeof(IExampleService) });
            binder.BindToName(typeof(ExampleProperties), out var assemblyName, out var typeName);
            var t = binder.BindToType(assemblyName, typeName);
            t.Should().Be(typeof(ExampleProperties));
        }

        public class DictionaryValue
        {
            public Guid Key { get; set; }
            
            public Char Chr { get; set; }
        }
        
        public class ExampleProperties
        {
            public string Field;
            
            public string PropertyGet { get; set; }
            
            public string PropertyGetSet { get; set; }
            
            public String[] Arguments { get; set; }
            
            public List<int> Ids { get; set; }
            
            public Dictionary<int,DictionaryValue> Set { get; set; }
        }

        public class ExampleWithObject
        {
            public string PropertyGetSet { get; set; }
            
            public object ObjectProperty  { get; set; }
        }

        public interface IHaveBase
        {
            float BaseMethod();
        }
        
        public interface IExampleService : IHaveBase
        {
            bool SimpleMethod(int port);

            string TupleMethod(Tuple<string, int> good);

            ExampleProperties ReturnComplex();

            void TakeComplex(ExampleProperties props);
        }

        [TestCase(typeof(IAsyncService))]
        [TestCase(typeof(IObjectResultService))]
        [TestCase(typeof(IObjectPropertyService))]
        [TestCase(typeof(IObjectExampleService))]
        public void Constructor_WithInvalidTypes_WillThrow(params Type[] types)
        {
            Assert.Throws<TypeNotAllowedException>(() => { _ = new RegisteredSerializationBinder(types); });
        }
        
        public interface IAsyncService
        { 
            Task<string> GetAsync();
        }

        public interface IObjectResultService
        {
            object GetMeSomething();
        }
        
        public interface IObjectPropertyService
        {
            void DoSomething(object data);
        }

        public interface IObjectExampleService
        {
            string DoThis(ExampleWithObject example);
        }
        
        [Test]
        public void Circular_Types_CanBeResolved()
        {
            var binder = new RegisteredSerializationBinder(new[] { typeof(IMCircular) });
            binder.BindToName(typeof(CircularPart1), out var assemblyName1, out var typeName1);
            var t1 = binder.BindToType(assemblyName1, typeName1);
            t1.Should().Be(typeof(CircularPart1));
            
            binder.BindToName(typeof(CircularPart2), out var assemblyName2, out var typeName2);
            var t2 = binder.BindToType(assemblyName2, typeName2);
            t2.Should().Be(typeof(CircularPart2));            
        }
        
        public class CircularPart1
        {
            public CircularPart2 Circular { get; set; }
        }
        
        public class CircularPart2
        {
            public CircularPart1 Circular { get; set; }
        }
        
        public interface IMCircular
        {
            CircularPart1 CircularPart1();
            
            CircularPart2 CircularPart2();
        }        
    }
}