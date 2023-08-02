using Halibut.Tests.Support.TestAttributes;

namespace Halibut.Tests.Support
{
    public static class ForceClientProxyTypeExtensionMethods
    {
        public static SyncOrAsync ToSyncOrAsync(this ForceClientProxyType? forceClientProxyType)
        {
            var syncOrAsync = forceClientProxyType == ForceClientProxyType.AsyncClient ? SyncOrAsync.Async : SyncOrAsync.Sync;
            return syncOrAsync;
        }
    }
}