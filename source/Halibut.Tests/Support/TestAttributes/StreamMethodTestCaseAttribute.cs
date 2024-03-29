﻿using System;
using System.Collections;
using Halibut.Tests.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class StreamMethodTestCaseAttribute : TestCaseSourceAttribute
    {
        public StreamMethodTestCaseAttribute(bool testSync = true) : base(
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
                yield return StreamMethod.LegacyAsyncCallEndWithinCallback;
                yield return StreamMethod.LegacyAsyncCallEndOutsideCallback;
            }
        }
    }
}