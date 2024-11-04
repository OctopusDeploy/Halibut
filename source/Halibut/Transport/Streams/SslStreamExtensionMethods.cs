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
                // Despite what you might think, this option allows the OS to decide the SSL version to use and is the recommended approach per the code comments on the version of SslProtocols we're using
                // This is current at time of writing. MS docs recommend using SslProtocols.SystemDefault which does not exist on this particular enum, so that's fun. 
                EnabledSslProtocols = SslProtocols.None,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };

            await ssl.AuthenticateAsClientAsync(options, linkedCts.Token);
        }
#endif
    }
}