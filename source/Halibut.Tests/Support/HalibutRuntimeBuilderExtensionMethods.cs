using Halibut.Transport.Streams;

namespace Halibut.Tests.Support
{
    public static class HalibutRuntimeBuilderExtensionMethods
    {
        public static HalibutRuntimeBuilder WithAsyncHalibutFeatureEnabledIfForcingAsync(this HalibutRuntimeBuilder halibutRuntimeBuilder, ForceClientProxyType? forceClientProxyType)
        {
            if (forceClientProxyType == ForceClientProxyType.AsyncClient)
            {
                return halibutRuntimeBuilder.WithAsyncHalibutFeatureEnabled();
            }

            return halibutRuntimeBuilder;
        }

        public static HalibutRuntimeBuilder WithStreamFactoryIfNotNull(this HalibutRuntimeBuilder halibutRuntimeBuilder, IStreamFactory? streamFactory)
        {
            if (streamFactory != null) halibutRuntimeBuilder.WithStreamFactory(streamFactory);
            return halibutRuntimeBuilder;
        }
    }
}