using System;

namespace Halibut.Diagnostics.LogCreators
{
    public static class CreateNewILogExtensionMethods
    {
        public static ILogFactory WithCaching(this ICreateNewILog nonCachingLogFactory)
        {
            return new CachingLogFactory(nonCachingLogFactory);
        }
    }
}