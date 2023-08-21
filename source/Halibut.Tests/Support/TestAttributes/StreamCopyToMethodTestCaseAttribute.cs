using System;
using System.Collections;
using Halibut.Tests.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class StreamCopyToMethodTestCaseAttribute : TestCaseSourceAttribute
    {
        public StreamCopyToMethodTestCaseAttribute(bool testSync = true) : base(
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
                    yield return StreamCopyToMethod.CopyTo;
                    yield return StreamCopyToMethod.CopyToWithBufferSize;
                }

                yield return StreamCopyToMethod.CopyToAsync;
                yield return StreamCopyToMethod.CopyToAsyncWithBufferSize;
            }
        }
    }
}