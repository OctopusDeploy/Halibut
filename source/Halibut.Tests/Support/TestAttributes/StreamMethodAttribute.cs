using System;
using System.Collections;
using System.Threading.Tasks;
using Halibut.Tests.Transport.Streams;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class StreamMethodAttribute : TestCaseSourceAttribute
    {
        public StreamMethodAttribute(bool testSync = true) : base(
            typeof(TestCases),
            nameof(TestCases.GetEnumerator),
            new object?[]{testSync})
        {
        }

        static class TestCases
        {
            public static IEnumerable GetEnumerator(bool testSync)
            {
                if (testSync)
                {
                    yield return StreamMethod.Sync;
                }
                
                yield return StreamMethod.Async;
                yield return StreamMethod.LegacyAsync;
            }
        }
    }
}