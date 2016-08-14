using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public class DiscoveryClient
    {
        static readonly byte[] HelloLine = Encoding.ASCII.GetBytes("HELLO" + Environment.NewLine + Environment.NewLine);

        public async Task<ServiceEndPoint> Discover(Uri remoteUri)
        {
            try
            {
                using (var client = CreateTcpClient())
                {
                    await client.ConnectWithTimeout(remoteUri, HalibutLimits.TcpClientConnectTimeout);
                    using (var stream = client.GetStream())
                    {
                        using (var ssl = new SslStream(stream, false, ValidateCertificate))
                        {
                            await ssl.AuthenticateAsClientAsync(remoteUri.Host, new X509Certificate2Collection(), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false).ConfigureAwait(false);
                            ssl.Write(HelloLine, 0, HelloLine.Length);
                            ssl.Flush();

                            if (ssl.RemoteCertificate == null)
                                throw new Exception("The server did not provide an SSL certificate");

                            return new ServiceEndPoint(remoteUri, new X509Certificate2(ssl.RemoteCertificate.Export(X509ContentType.Cert)).Thumbprint);
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