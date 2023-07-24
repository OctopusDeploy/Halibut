using System;
using System.Collections.Generic;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestAndPreviousClientAndServiceVersionsTestCasesAttribute : TestCaseSourceAttribute
    {
        public LatestAndPreviousClientAndServiceVersionsTestCasesAttribute(bool testWebSocket = true, bool testNetworkConditions = true) :
            base(
                typeof(LatestAndPreviousClientAndServiceVersionsTestCases),
                nameof(LatestAndPreviousClientAndServiceVersionsTestCases.GetEnumerator),
                new object[] { testWebSocket, testNetworkConditions })
        {
        }
        
        static class LatestAndPreviousClientAndServiceVersionsTestCases
        {
            public static IEnumerator<ClientAndServiceTestCase> GetEnumerator(bool testWebSocket, bool testNetworkConditions)
            {
                var builder = new ClientAndServiceTestCasesBuilder(
                    new[] {
                        ClientAndServiceTestVersion.Latest(),
                        ClientAndServiceTestVersion.ClientOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ClientVersion),
                        ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ServiceVersion),
                    },
                    testWebSocket ? ServiceConnectionTypes.All : ServiceConnectionTypes.AllExceptWebSockets,
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect }
                );

                return builder.Build().GetEnumerator();
            }
        }
    }
}
