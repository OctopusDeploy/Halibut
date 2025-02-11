using System.Security.Authentication;

namespace Halibut.Transport
{
    public static class SslConfiguration
    {
        public static SslProtocols SupportedProtocols
        {
#pragma warning disable SYSLIB0039
            // See https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0039
            // TLS 1.0 and 1.1 are obsolete from .NET 7
            get => SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
#pragma warning restore SYSLIB0039
        }
    }
}