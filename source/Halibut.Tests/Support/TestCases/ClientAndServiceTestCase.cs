using System.Text;

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
        
        public ClientAndServiceTestCase(ServiceConnectionType serviceConnectionType,
            NetworkConditionTestCase networkConditionTestCase,
            int recommendedIterations,
            ClientAndServiceTestVersion clientAndServiceTestVersion)
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

        public IClientOnlyBuilder CreateClientOnlyTestCaseBuilder()
        {
            var logger = new SerilogLoggerBuilder().Build();
            var builder = ClientAndServiceBuilderFactory.ForVersionClientOnly(ClientAndServiceTestVersion)(ServiceConnectionType);

            if (NetworkConditionTestCase.PortForwarderFactory != null)
            {
                builder.WithPortForwarding(out _, i => NetworkConditionTestCase.PortForwarderFactory(i, logger));
            }

            return builder;
        }

        public IServiceOnlyBuilder CreateServiceOnlyTestCaseBuilder()
        {
            var logger = new SerilogLoggerBuilder().Build();
            var builder = ClientAndServiceBuilderFactory.ForVersionServiceOnly(ClientAndServiceTestVersion)(ServiceConnectionType);

            if (NetworkConditionTestCase.PortForwarderFactory != null)
            {
                builder.WithPortForwarding(out _, i => NetworkConditionTestCase.PortForwarderFactory(i, logger));
            }

            return builder;
        }

        public override string ToString()
        {
            // This is used as the test parameter name, so make this something someone can understand in teamcity or their IDE.
            var builder = new StringBuilder();
            
            // These names can be longer than what rider will handle, setting UseShortTestNames=true will help with that.
            bool useShortString = EnvironmentVariableReaderHelper.EnvironmentVariableAsBool("UseShortTestNames", false);
            
            builder.Append(ServiceConnectionType.ToString());
            builder.Append(", ");
            builder.Append(useShortString ? ClientAndServiceTestVersion.ToShortString(ServiceConnectionType): ClientAndServiceTestVersion.ToString(ServiceConnectionType));
            builder.Append(", ");
            builder.Append(useShortString ? NetworkConditionTestCase.ToShortString() : NetworkConditionTestCase.ToString());
            builder.Append(", ");
            builder.Append(useShortString ? $"RecIters:{RecommendedIterations}" : $"RecommendedIters: {RecommendedIterations}");
            
            return builder.ToString();
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ClientAndServiceTestCase);
        }

        public bool Equals(ClientAndServiceTestCase? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return this.ToString().Equals(other.ToString());
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}
