using System;
using System.Collections;
using Halibut.Tests.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class StreamReadMethodTestCaseAttribute : TestCaseSourceAttribute
    {
        public StreamReadMethodTestCaseAttribute(bool testSync = true) : base(
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
                    yield return StreamReadMethod.Read;
                    yield return StreamReadMethod.ReadByte;
                    yield return StreamReadMethod.BeginReadEndOutsideCallback;
                    yield return StreamReadMethod.BeginReadEndWithinCallback;
                }

                yield return StreamReadMethod.ReadAsync;
#if !NETFRAMEWORK
                yield return StreamReadMethod.ReadAsyncForMemoryByteArray;
#endif
            }
        }
    }
}