using System;

namespace Halibut.Tests.Support.TestCases
{
    public class LatestClientAndServiceTestCase
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly NetworkConditionTestCase networkConditionTestCase;

        public ServiceConnectionType ServiceConnectionType => serviceConnectionType;

        public LatestClientAndServiceTestCase(ServiceConnectionType serviceConnectionType, NetworkConditionTestCase networkConditionTestCase)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.networkConditionTestCase = networkConditionTestCase;
        }

        public IClientAndServiceBuilder CreateBaseTestCaseBuilder()
        {
            var logger = new SerilogLoggerBuilder().Build();
            IClientAndServiceBuilder builder = ClientServiceBuilder.ForServiceConnectionType(serviceConnectionType);
            if (networkConditionTestCase.PortForwarderFactory != null)
            {
                builder.WithPortForwarding(i => networkConditionTestCase.PortForwarderFactory(i, logger));
            }

            return builder;
        }

        public override string ToString()
        {
            return serviceConnectionType + ", " + networkConditionTestCase.ToString();
        }
    }
}
