using System;
using System.Collections.Generic;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Util;

namespace Halibut.Tests.Support.TestCases
{
    public class ClientAndServiceTestCase
    {
        public ClientAndServiceTestVersion ClientAndServiceTestVersion { get; }

        public NetworkConditionTestCase NetworkConditionTestCase { get; }

        public ServiceConnectionType ServiceConnectionType { get; }

        public AsyncHalibutFeature ServiceAsyncHalibutFeature { get; }

        /// <summary>
        ///     If running a test which wants to make the same or similar calls multiple time, this has a "good"
        ///     number of times to do that. It takes care of generally not picking such a high number the tests
        ///     don't take a long time.
        /// </summary>
        public int RecommendedIterations { get; }

        public ForceClientProxyType? ForceClientProxyType { get; }
        public SyncOrAsync SyncOrAsync => ForceClientProxyType.ToSyncOrAsync();

        public ClientAndServiceTestCase(ServiceConnectionType serviceConnectionType,
            NetworkConditionTestCase networkConditionTestCase,
            int recommendedIterations,
            ClientAndServiceTestVersion clientAndServiceTestVersion,
            ForceClientProxyType? forceClientProxyType,
            AsyncHalibutFeature serviceAsyncHalibutFeature)
        {
            ServiceConnectionType = serviceConnectionType;
            NetworkConditionTestCase = networkConditionTestCase;
            RecommendedIterations = recommendedIterations;
            ClientAndServiceTestVersion = clientAndServiceTestVersion;
            ForceClientProxyType = forceClientProxyType;
            ServiceAsyncHalibutFeature = serviceAsyncHalibutFeature;
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

            if (ServiceAsyncHalibutFeature == AsyncHalibutFeature.Enabled)
            {
                builder.WithServiceAsyncHalibutFeatureEnabled();
            }

            return builder;
        }
        
        public override string ToString()
        {
            // This is used as the test parameter name, so make this something someone can understand in teamcity or their IDE.
            var testParameter = new List<string>();
            
            // These names can be longer than what rider will handle, setting UseShortTestNames=true will help with that.
            bool useShortString = EnvironmentVariableReaderHelper.EnvironmentVariableAsBool("UseShortTestNames", false);
            
            testParameter.Add(ServiceConnectionType.ToString());
            testParameter.Add(useShortString ? ClientAndServiceTestVersion.ToShortString(ServiceConnectionType): ClientAndServiceTestVersion.ToString(ServiceConnectionType));
            testParameter.Add(useShortString ? NetworkConditionTestCase.ToShortString() : NetworkConditionTestCase.ToString());
            testParameter.Add(useShortString ? $"RecIters:{RecommendedIterations}" : $"RecommendedIters: {RecommendedIterations}");
            if (ForceClientProxyType != null)
            {
                testParameter.Add(ForceClientProxyType.ToString());
            }

            if (ServiceAsyncHalibutFeature == AsyncHalibutFeature.Enabled)
            {
                testParameter.Add("AsyncService");
            }
            else
            {
                testParameter.Add("SyncService");
            }
            
            return string.Join(", ", testParameter);
        }
    }
}
