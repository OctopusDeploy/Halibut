using System;

namespace Halibut.Diagnostics.LogCreators
{
    public static class CreateNewILogExtensionMethods
    {
        public static ILogFactory ToCachingLogFactory(this ICreateNewILog nonCachingLogFactory)
        {
            return new CachingLogFactory(nonCachingLogFactory);
        }
    }
}