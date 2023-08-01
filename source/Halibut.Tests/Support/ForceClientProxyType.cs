using System;

namespace Halibut.Tests.Support
{
    public enum ForceClientProxyType
    {
        SyncClient,
        AsyncClient
    }

    public static class ForceClientProxyTypeValues
    {
        public static ForceClientProxyType[] All = {ForceClientProxyType.SyncClient, ForceClientProxyType.AsyncClient};
    }
}