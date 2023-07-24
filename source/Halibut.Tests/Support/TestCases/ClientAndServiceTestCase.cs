using System;

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

        public ClientAndServiceTestCase(ServiceConnectionType serviceConnectionType, NetworkConditionTestCase networkConditionTestCase, int recommendedIterations, ClientAndServiceTestVersion clientAndServiceTestVersion)
        {
            ServiceConnectionType = serviceConnectionType;
            NetworkConditionTestCase = networkConditionTestCase;
            RecommendedIterations = recommendedIterations;
            ClientAndServiceTestVersion = clientAndServiceTestVersion;
        }

        public IClientAndServiceBuilder CreateTestCaseBuilder()
        {
            var logger = new SerilogLoggerBuilder().Build();
            var builder = ClientAndServiceBuilderFactory.ForVersion(ClientAndServiceTestVersion)(ServiceConnectionType);
            
            if (NetworkConditionTestCase.PortForwarderFactory != null)
            {
                builder.WithPortForwarding(i => NetworkConditionTestCase.PortForwarderFactory(i, logger));
            }

            return builder;
        }
        
        public override string ToString()
        {
            // This is used as the test parameter name, so make this something someone can understand in teamcity or their IDE.
            return $"{ServiceConnectionType}, {ClientAndServiceTestVersion.ToString(ServiceConnectionType)}, {NetworkConditionTestCase}, RecommendedIterations: {RecommendedIterations}";
        }
    }
}
