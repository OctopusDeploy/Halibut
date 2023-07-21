namespace Halibut.Tests.Support
{
    public static class ClientAndServiceBuilderExtensionMethods
    {
        public static LatestClientAndLatestServiceBuilder AsLatestClientAndLatestServiceBuilder(this IClientAndServiceBuilder clientAndServiceBuilder)
        {
            return (LatestClientAndLatestServiceBuilder) clientAndServiceBuilder;
        }
    }
}