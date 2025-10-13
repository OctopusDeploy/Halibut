namespace Halibut.Transport
{
    public static class SslConfiguration
    {
        public static ISslConfigurationProvider Default { get; } = new DefaultSslConfigurationProvider();
    }
}