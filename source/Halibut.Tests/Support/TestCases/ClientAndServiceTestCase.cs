using System;

namespace Halibut.Tests.Support.TestCases
{
    public class ClientAndServiceTestCase
    {
        ClientAndServiceTestVersion _clientAndServiceTestVersion;
        
        readonly NetworkConditionTestCase networkConditionTestCase;
        public ServiceConnectionType ServiceConnectionType { get; }
        

        /// <summary>
        ///     If running a test which wants to make the same or similar calls multiple time, this has a "good"
        ///     number of times to do that. It takes care of generally not picking such a high number the tests
        ///     don't take a long time.
        /// </summary>
        public readonly int RecommendedIterations;

        public ClientAndServiceTestCase(ServiceConnectionType serviceConnectionType, NetworkConditionTestCase networkConditionTestCase, int recommendedIterations, ClientAndServiceTestVersion clientAndServiceTestVersion)
        {
            ServiceConnectionType = serviceConnectionType;
            this.networkConditionTestCase = networkConditionTestCase;
            RecommendedIterations = recommendedIterations;
            this._clientAndServiceTestVersion = clientAndServiceTestVersion;
        }

        public IClientAndServiceBuilder CreateBaseTestCaseBuilder()
        {
            var logger = new SerilogLoggerBuilder().Build();
            IClientAndServiceBuilder builder = ClientAndServiceBuilderFactory.ForVersion(_clientAndServiceTestVersion)(ServiceConnectionType);
            if (networkConditionTestCase.PortForwarderFactory != null)
            {
                builder.WithPortForwarding(i => networkConditionTestCase.PortForwarderFactory(i, logger));
            }

            return builder;
        }

        public override string ToString()
        {
            return ServiceConnectionType + ", " + _clientAndServiceTestVersion + ", " + networkConditionTestCase + ", Iters: " + RecommendedIterations;
        }
    }
}
