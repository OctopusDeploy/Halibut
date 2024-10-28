using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    static class SslStreamExtensionMethods
    {
#if !NETFRAMEWORK
        internal static async Task AuthenticateAsClientEnforcingTimeout(
            this SslStream ssl, 
            ServiceEndPoint serviceEndpoint,
            X509Certificate2Collection clientCertificates, 
            CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(ssl.ReadTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            var options = new SslClientAuthenticationOptions
            {
                TargetHost = serviceEndpoint.BaseUri.Host,
                ClientCertificates = clientCertificates,
#pragma warning disable SYSLIB0039
                EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
#pragma warning restore SYSLIB0039
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };

            await ssl.AuthenticateAsClientAsync(options, linkedCts.Token);
        }
#endif
    }
}