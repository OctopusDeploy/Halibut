using System;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Halibut.Tests.Support.ExtensionMethods
{
    static class TestContextExtensionMethods
    {
        public static TestExecutionContext GetTestExecutionContext(this TestContext testContext)
        {
            var testExecutionContextField = testContext.GetType().GetField("_testExecutionContext", BindingFlags.NonPublic | BindingFlags.Instance);
            var testExecutionContext = (TestExecutionContext)testExecutionContextField!.GetValue(testContext);

            return testExecutionContext;
        }

        public static TimeSpan? GetTestTimeout(this TestContext testContext)
        {
            var timeout = TestContext.CurrentContext.GetTestExecutionContext().TestCaseTimeout;

            if (timeout > 0)
            {
                return TimeSpan.FromMilliseconds(timeout);
            }

            return null;
        }
    }
}
