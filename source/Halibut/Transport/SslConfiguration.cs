using System;
using System.Security.Authentication;

namespace Halibut.Transport
{
    public static class SslConfiguration
    {
        static SslProtocols GetSupportedProtocols()
        {
#if NETFRAMEWORK
            // Net48 tests has issues establishing a common algorithm when we allow system default
            var supportedProtocolMode = "legacy";
#else
            var supportedProtocolMode = Environment.GetEnvironmentVariable("HALIBUT_SUPPORTED_SSL_PROTOCOLS")?.ToLowerInvariant();
#endif


            if (supportedProtocolMode == "legacy")
            {
#pragma warning disable SYSLIB0039
                // See https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0039
                // TLS 1.0 and 1.1 are obsolete from .NET 7
                return SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
#pragma warning restore SYSLIB0039
            }

            if (supportedProtocolMode == "system")
            {
                return SslProtocols.None;
            }

            if (supportedProtocolMode == "modern")
            {
                return SslProtocols.Tls12 | SslProtocols.Tls13;
            }

            if (supportedProtocolMode == "tls1.3")
            {
                return SslProtocols.Tls13;
            }

            return SslProtocols.None;
        }

        public static SslProtocols SupportedProtocols { get; } = GetSupportedProtocols();
    }
}