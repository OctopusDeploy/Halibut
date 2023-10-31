using Halibut.Transport.Streams;

namespace Halibut.Tests.Support
{
    public static class HalibutRuntimeBuilderExtensionMethods
    {
        public static HalibutRuntimeBuilder WithStreamFactoryIfNotNull(this HalibutRuntimeBuilder halibutRuntimeBuilder, IStreamFactory? streamFactory)
        {
            if (streamFactory != null) halibutRuntimeBuilder.WithStreamFactory(streamFactory);
            return halibutRuntimeBuilder;
        }
    }
}