using System;
using System.Collections.Generic;
using Halibut.Tests.Support.TestAttributes;

namespace Halibut.Tests.Support.TestCases
{
    public class ClientAndServiceTestCase
    {
        public ClientAndServiceTestVersion ClientAndServiceTestVersion { get; }

        public NetworkConditionTestCase NetworkConditionTestCase { get; }

        public ServiceConnectionType ServiceConnectionType { get; }

        /// <summary>
        ///     If running a test which wants to make the same or similar calls multiple time, this has a "good"
        ///     number of times to do that. It takes care of generally not picking such a high number the tests
        ///     don't take a long time.
        /// </summary>
        public int RecommendedIterations { get; }

        public ForceClientProxyType? ForceClientProxyType { get; }
        public SyncOrAsync SyncOrAsync => ForceClientProxyType.ToSyncOrAsync();

        public ClientAndServiceTestCase(ServiceConnectionType serviceConnectionType, NetworkConditionTestCase networkConditionTestCase, int recommendedIterations, ClientAndServiceTestVersion clientAndServiceTestVersion, ForceClientProxyType? forceClientProxyType)
        {
            ServiceConnectionType = serviceConnectionType;
            NetworkConditionTestCase = networkConditionTestCase;
            RecommendedIterations = recommendedIterations;
            ClientAndServiceTestVersion = clientAndServiceTestVersion;
            ForceClientProxyType = forceClientProxyType;
        }

        public IClientAndServiceBuilder CreateTestCaseBuilder()
        {
            var logger = new SerilogLoggerBuilder().Build();
            var builder = ClientAndServiceBuilderFactory.ForVersion(ClientAndServiceTestVersion)(ServiceConnectionType);
            
            if (NetworkConditionTestCase.PortForwarderFactory != null)
            {
                builder.WithPortForwarding(i => NetworkConditionTestCase.PortForwarderFactory(i, logger));
            }

            if (ForceClientProxyType != null)
            {
                builder.WithForcingClientProxyType(ForceClientProxyType.Value);
            }

            return builder;
        }
        
        public override string ToString()
        {
            // This is used as the test parameter name, so make this something someone can understand in teamcity or their IDE.
            var testParameter = new List<string>();
            testParameter.Add(ServiceConnectionType.ToString());
            testParameter.Add(ClientAndServiceTestVersion.ToString(ServiceConnectionType));
            testParameter.Add(NetworkConditionTestCase.ToString());
            testParameter.Add($"RecommendedIters: {RecommendedIterations}");
            if (ForceClientProxyType != null)
            {
                testParameter.Add(ForceClientProxyType.ToString());
            }
            
            return string.Join(", ", testParameter);
        }
    }
}
