using System;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    /// <summary>
    /// Indicates the source to be used to provide test fixture instances for a test class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class TestCasesAttribute : TestCaseSourceAttribute
    {
        public TestCasesAttribute(Type sourceType, string sourceName, object?[]? methodParams) :
            base (sourceType, sourceName, methodParams)
        {
        }
    }
}