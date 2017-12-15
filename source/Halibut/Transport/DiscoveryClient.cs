using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Halibut.Diagnostics;
using Halibut.Transport.Proxy;

namespace Halibut.Transport
{
    public class DiscoveryClient
    {
        static readonly byte[] HelloLine = Encoding.ASCII.GetBytes("HELLO" + Environment.NewLine + Environment.NewLine);
        readonly ILogFactory logs = LogFactoryContext.CurrentLogFactory;

        public ServiceEndPoint Discover(ServiceEndPoint serviceEndpoint)
        {
            try
            {
                using (var client = CreateConnectedTcpClient(serviceEndpoint))
                {
                    using (var stream = client.GetStream())
                    {
                        using (var ssl = new SslStream(stream, false, ValidateCertificate))
                        {
                            ssl.AuthenticateAsClientAsync(serviceEndpoint.BaseUri.Host, new X509Certificate2Collection(), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false)
                                .GetAwaiter()
                                .GetResult();
                            ssl.Write(HelloLine, 0, HelloLine.Length);
                            ssl.Flush();

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

        TcpClient CreateConnectedTcpClient(ServiceEndPoint endPoint)
        {
            TcpClient client;
            if (endPoint.Proxy == null)
            {
                client = CreateTcpClient();
                client.ConnectWithTimeout(endPoint.BaseUri, HalibutLimits.TcpClientConnectTimeout);
            }
            else
            {
                var log = logs.ForEndpoint(endPoint.BaseUri);
                log.Write(EventType.Diagnostic, "Creating a proxy client");
                client = new ProxyClientFactory()
                    .CreateProxyClient(log, endPoint.Proxy)
                    .WithTcpClientFactory(CreateTcpClient)
                    .CreateConnection(endPoint.BaseUri.Host, endPoint.BaseUri.Port, HalibutLimits.TcpClientConnectTimeout);
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