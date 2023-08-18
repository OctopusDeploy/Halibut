using System;
using System.Collections;
using Halibut.Tests.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class StreamWriteMethodTestCaseAttribute : TestCaseSourceAttribute
    {
        public StreamWriteMethodTestCaseAttribute(bool testSync = true) : base(
            typeof(TestCases),
            nameof(TestCases.GetEnumerator),
            new object[] { testSync })
        {
        }

        static class TestCases
        {
            public static IEnumerable GetEnumerator(bool testSync)
            {
                if (testSync)
                {
                    yield return StreamWriteMethod.Write;
                    yield return StreamWriteMethod.WriteByte;
                    yield return StreamWriteMethod.BeginWriteEndOutsideCallback;
                    yield return StreamWriteMethod.BeginWriteEndWithinCallback;
                }

                yield return StreamWriteMethod.WriteAsync;
#if !NETFRAMEWORK
                yield return StreamWriteMethod.WriteAsyncForMemoryByteArray;
#endif
            }
        }
    }
}