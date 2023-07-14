using System;
using System.Collections.Generic;
using Halibut.Tests.Support.TestCases;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    /// <summary>
    ///     Holds all the standard test cases for testing a latest client with a latest service.
    ///     In this case latest means the code as is, rather than some previously built version of halibut.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestClientAndLatestServiceTestCasesAttribute : TestCaseSourceAttribute
    {
        public LatestClientAndLatestServiceTestCasesAttribute(bool testWebSocket = true, bool testNetworkConditions = true) :
            base(
                typeof(LatestClientAndLatestServiceTestCases), 
                nameof(LatestClientAndLatestServiceTestCases.GetEnumerator), 
                new object[]{ testWebSocket, testNetworkConditions })
        {
        }
        
        static class LatestClientAndLatestServiceTestCases
        {
            public static IEnumerator<ClientAndServiceTestCase> GetEnumerator(bool testWebSocket, bool testNetworkConditions)
            {
                var builder = new ClientAndServiceTestCasesBuilder(
                    new[] { ClientAndServiceTestVersion.Latest() },
                    testWebSocket ? ServiceConnectionTypes.All : ServiceConnectionTypes.AllExceptWebSockets,
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect }
                );

                return builder.Build().GetEnumerator();
            }
        }
    }
}
