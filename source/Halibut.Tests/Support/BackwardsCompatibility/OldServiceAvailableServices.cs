namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class OldServiceAvailableServices
    {
        public OldServiceAvailableServices(bool hasStandardServices, bool hasCachingService)
        {
            HasStandardServices = hasStandardServices;
            HasCachingService = hasCachingService;
        }

        public bool HasStandardServices { get; set; }
        public bool HasCachingService { get; set; }
    }
}