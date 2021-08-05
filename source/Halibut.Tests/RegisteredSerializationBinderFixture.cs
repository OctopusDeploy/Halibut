using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class RegisteredSerializationBinderFixture
    {
        public class ExampleProperties
        {
            public string Field;
            
            public string PropertyGet { get; set; }
            
            public string PropertyGetSet { get; set; }
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

        public interface IAsyncService
        { 
            Task<string> GetAsync();
        }

        public interface IObjectResultSerice
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
        public void BindMethods_WithValidClass_FindsAllMethodTypes()
        {
            var binder = new RegisteredSerializationBinder(new[] { typeof(IExampleService) });
            binder.BindToName(typeof(ExampleProperties), out var assemblyName, out var typeName);
            var t = binder.BindToType(assemblyName, typeName);
            t.Should().Be(typeof(ExampleProperties));
        }

        [TestCase(typeof(IAsyncService))]
        [TestCase(typeof(IObjectResultSerice))]
        [TestCase(typeof(IObjectPropertyService))]
        [TestCase(typeof(IObjectExampleService))]
        public void Constructor_WithInvalidTypes_WillThrow(params Type[] types)
        {
            Assert.Throws<TypeNotAllowedException>(() => { _ = new RegisteredSerializationBinder(types); });
        }
    }
}