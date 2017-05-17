using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Proxy;

namespace Halibut.Transport
{
    public class DiscoveryClient
    {
        static readonly byte[] HelloLine = Encoding.ASCII.GetBytes("HELLO" + Environment.NewLine + Environment.NewLine);
        readonly LogFactory logs = new LogFactory();

        public async Task<ServiceEndPoint> Discover(ServiceEndPoint serviceEndpoint)
        {
            try
            {
                using (var client = await CreateConnectedTcpClient(serviceEndpoint).ConfigureAwait(false))
                {
                    using (var stream = client.GetStream())
                    {
                        using (var ssl = new SslStream(stream, false, ValidateCertificate))
                        {
                            await ssl.AuthenticateAsClientAsync(serviceEndpoint.BaseUri.Host, new X509Certificate2Collection(), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false).ConfigureAwait(false);
                            await ssl.WriteAsync(HelloLine, 0, HelloLine.Length).ConfigureAwait(false);
                            await ssl.FlushAsync().ConfigureAwait(false);

                            if (ssl.RemoteCertificate == null)
                                throw new Exception("The server did not provide an SSL certificate");

                            return new ServiceEndPoint(serviceEndpoint.BaseUri, new X509Certificate2(ssl.RemoteCertificate.Export(X509ContentType.Cert)).Thumbprint);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new HalibutClientException(ex.Message, ex);
            }
        }

        bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return true;
        }

        async Task<TcpClient> CreateConnectedTcpClient(ServiceEndPoint endPoint)
        {
            TcpClient client;
            if (endPoint.Proxy == null)
            {
                client = CreateTcpClient();
                await client.ConnectWithTimeout(endPoint.BaseUri, HalibutLimits.TcpClientConnectTimeout).ConfigureAwait(false);
            }
            else
            {
                var log = logs.ForEndpoint(endPoint.BaseUri);
                log.Write(EventType.Diagnostic, "Creating a proxy client");
                client = await new ProxyClientFactory()
                    .CreateProxyClient(log, endPoint.Proxy)
                    .WithTcpClientFactory(CreateTcpClient)
                    .CreateConnection(endPoint.BaseUri.Host, endPoint.BaseUri.Port, HalibutLimits.TcpClientConnectTimeout)
                    .ConfigureAwait(false);
            }

            return client;
        }

        static TcpClient CreateTcpClient()
        {
            var client = new TcpClient(AddressFamily.InterNetworkV6)
            {
                SendTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalMilliseconds,
                ReceiveTimeout = (int)HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds,
                Client = { DualMode = true }
            };
            return client;
        }
    }
}