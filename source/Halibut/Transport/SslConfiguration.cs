namespace Halibut.Transport
{
    public static class SslConfiguration
    {
        public static ISslConfigurationProvider Default { get; }
#if NETFRAMEWORK // .NET4.8 exhibited inconsistent behavior when using the default configuration
            = new LegacySslConfigurationProvider();
#else
            = new DefaultSslConfigurationProvider();
#endif
    }
}