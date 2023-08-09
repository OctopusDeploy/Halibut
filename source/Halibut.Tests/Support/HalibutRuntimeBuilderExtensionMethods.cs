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
    }
}