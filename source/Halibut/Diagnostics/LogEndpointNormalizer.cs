using System;

namespace Halibut.Diagnostics
{
    public static class LogEndpointNormalizer
    {
        public static Uri NormalizeEndpointForLogging(Uri endpoint)
        {
            return ServiceEndPoint.IsWebSocketAddress(endpoint)
                ? new Uri(endpoint.AbsoluteUri.ToLowerInvariant())
                : new Uri(endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/').ToLowerInvariant());
        }
    }
}