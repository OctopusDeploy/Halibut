using System.Security.Authentication;

namespace Halibut.Transport
{
    public static class SslConfiguration
    {
        public static SslProtocols SupportedProtocols => SslProtocols.None;  // None means system defaults
    }
}