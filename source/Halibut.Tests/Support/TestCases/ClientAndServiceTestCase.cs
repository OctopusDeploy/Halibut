using System;

namespace Halibut.Tests.Support.TestCases
{
    public class ClientAndServiceTestCase
    {
        ClientServiceTestVersion clientServiceTestVersion;
        
        readonly NetworkConditionTestCase networkConditionTestCase;
        public ServiceConnectionType ServiceConnectionType { get; }
        

        /// <summary>
        ///     If running a test which wants to make the same or similar calls multiple time, this has a "good"
        ///     number of times to do that. It takes care of generally not picking such a high number the tests
        ///     don't take a long time.
        /// </summary>
        public readonly int RecommendedIterations;

        public ClientAndServiceTestCase(ServiceConnectionType serviceConnectionType, NetworkConditionTestCase networkConditionTestCase, int recommendedIterations, ClientServiceTestVersion clientServiceTestVersion)
        {
            ServiceConnectionType = serviceConnectionType;
            this.networkConditionTestCase = networkConditionTestCase;
            RecommendedIterations = recommendedIterations;
            this.clientServiceTestVersion = clientServiceTestVersion;
        }

        public IClientAndServiceBaseBuilder CreateBaseTestCaseBuilder()
        {
            var logger = new SerilogLoggerBuilder().Build();
            IClientAndServiceBaseBuilder builder = ClientServiceBuilderFactory.ForVersion(clientServiceTestVersion)(ServiceConnectionType);
            if (networkConditionTestCase.PortForwarderFactory != null)
            {
                builder.WithPortForwarding(i => networkConditionTestCase.PortForwarderFactory(i, logger));
            }

            return builder;
        }

        public override string ToString()
        {
            return ServiceConnectionType + ", " + clientServiceTestVersion + ", " + networkConditionTestCase + ", Iters: " + RecommendedIterations;
        }
    }
}
