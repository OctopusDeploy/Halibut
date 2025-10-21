namespace Halibut.Transport
{
    public static class SslConfiguration
    {
        public static ISslConfigurationProvider Default { get; }
#if NETFRAMEWORK
            = new LegacySslConfigurationProvider();
#else
            = new DefaultSslConfigurationProvider();
#endif
    }
}