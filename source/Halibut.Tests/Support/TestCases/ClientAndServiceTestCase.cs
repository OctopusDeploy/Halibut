using System;
using System.Collections.Generic;
using System.Text;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Util;
using Xunit.Abstractions;

namespace Halibut.Tests.Support.TestCases
{
    public class ClientAndServiceTestCase : IXunitSerializable
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

        public ClientAndServiceTestCase()
        {
        }

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
            builder.Append(", ");
            if (ForceClientProxyType != null)
            {
                builder.Append(ForceClientProxyType.ToString());
                builder.Append(", ");
            }

            if (ServiceAsyncHalibutFeature == AsyncHalibutFeature.Enabled)
            {
                builder.Append("AsyncService");
            }
            else
            {
                builder.Append("SyncService");
            }
            
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

        public void Deserialize(IXunitSerializationInfo info)
        {
        }

        public void Serialize(IXunitSerializationInfo info)
        {
        }
    }
}
