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
                EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };

            await ssl.AuthenticateAsClientAsync(options, linkedCts.Token);
        }
#endif
    }
}