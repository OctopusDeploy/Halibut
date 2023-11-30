using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Streams;

namespace Halibut.Transport
{
    public class DiscoveryClient
    {
        static readonly byte[] HelloLine = Encoding.ASCII.GetBytes("HELLO" + Environment.NewLine + Environment.NewLine);
        readonly LogFactory logs = new ();

        readonly IStreamFactory streamFactory;

        public DiscoveryClient(IStreamFactory streamFactory)
        {
            this.streamFactory = streamFactory;
        }

        public async Task<ServiceEndPoint> DiscoverAsync(ServiceEndPoint serviceEndpoint, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, CancellationToken cancellationToken)
        {
            try
            {
                var log = logs.ForEndpoint(serviceEndpoint.BaseUri);
                using (var client = await TcpConnectionFactory.CreateConnectedTcpClientAsync(serviceEndpoint, halibutTimeoutsAndLimits, streamFactory, log, cancellationToken))
                {
#if !NETFRAMEWORK
                    await
#endif
                    using (var networkTimeoutStream = streamFactory.CreateStream(client))
                    {
#if !NETFRAMEWORK
                        await
#endif                        
                        using (var ssl = new SslStream(networkTimeoutStream, false, ValidateCertificate))
                        {
#if NETFRAMEWORK
                            // TODO: ASYNC ME UP!
                            // AuthenticateAsClientAsync in .NET 4.8 does not support cancellation tokens. So `cancellationToken` is not respected here.
                            await ssl.AuthenticateAsClientAsync(serviceEndpoint.BaseUri.Host, new X509Certificate2Collection(), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);
#else
                            await ssl.AuthenticateAsClientEnforcingTimeout(serviceEndpoint, new X509Certificate2Collection(), cancellationToken);
#endif
                            await ssl.WriteAsync(HelloLine, 0, HelloLine.Length, cancellationToken);
                            await ssl.FlushAsync(cancellationToken);

                            if (ssl.RemoteCertificate == null)
                                throw new Exception("The server did not provide an SSL certificate");

#pragma warning disable PC001 // API not supported on all platforms - X509Certificate2 not supported on macOS
                            return new ServiceEndPoint(serviceEndpoint.BaseUri, new X509Certificate2(ssl.RemoteCertificate.Export(X509ContentType.Cert), (string)null!).Thumbprint, halibutTimeoutsAndLimits);
#pragma warning restore PC001 // API not supported on all platforms - X509Certificate2 not supported on macOS
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new HalibutClientException(ex.Message, ex);
            }
        }

        bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslpolicyerrors)
        {
            return true;
        }
    }
}
