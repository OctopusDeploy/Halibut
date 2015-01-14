using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Protocol;
using Halibut.Services;

namespace Halibut.Server
{
    public class SecureListener
    {
        readonly IPEndPoint endPoint;
        readonly X509Certificate2 serverCertificate;
        readonly IMessageExchangeParticipant messageExchange;
        readonly Func<RequestMessage, ResponseMessage> requestHandler;
        TcpListener listener;
        bool isStopped;

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, IMessageExchangeParticipant messageExchange, Func<RequestMessage, ResponseMessage> requestHandler)
        {
            this.endPoint = endPoint;
            this.serverCertificate = serverCertificate;
            this.messageExchange = messageExchange;
            this.requestHandler = requestHandler;

            EnsureCertificateIsValidForListening(serverCertificate);
        }

        public int Start()
        {
            listener = new TcpListener(endPoint);
            listener.Start();
            Accept();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        void Accept()
        {
            if (isStopped)
                return;

            listener.BeginAcceptTcpClient(r =>
            {
                try
                {
                    var client = listener.EndAcceptTcpClient(r);
                    if (isStopped)
                        return;

                    Logs.Server.Info("Accepted TCP client " + client.Client.RemoteEndPoint);

                    var task = new Task(() => ExecuteRequest(client));
                    task.Start();
                }
                catch (Exception ex)
                {
                    Logs.Server.Warn("TCP client error: " + ex.ToString());
                }

                Accept();
            }, null);
        }

        void ExecuteRequest(TcpClient client)
        {
            var clientName = client.Client.RemoteEndPoint;
            using (var stream = client.GetStream())
            using (var ssl = new SslStream(stream, false, ValidateCertificate))
            {
                try
                {
                    ssl.AuthenticateAsServer(serverCertificate, false, SslProtocols.Tls, false);

                    var reader = new StreamReader(ssl);
                    var firstLine = reader.ReadLine();
                    string nextLine;
                    while (!string.IsNullOrWhiteSpace(reader.ReadLine()))
                    {
                    }

                    if (firstLine != "MX")
                    {
                        SendFriendlyHtmlPage(ssl);
                    }
                    else
                    {
                        ProcessMessages(ssl);
                    }
                }
                catch (AuthenticationException ex)
                {
                    Logs.Server.Warn("Client " + clientName + " failed authentication: " + ex);
                }
                catch (Exception ex)
                {
                    Logs.Server.ErrorFormat("Unhandled error when handling request from client {0}: {1}", clientName, ex);
                }
            }

            client.Close();
        }

        void ProcessMessages(SslStream ssl)
        {
            var protocol = new MessageExchangeProtocol(messageExchange, requestHandler);
            protocol.IdentifyAsServer(null, ssl);

            while (true)
            {
                protocol.ExchangeAsServer(ssl);
            }
        }

        static void SendFriendlyHtmlPage(SslStream ssl)
        {
            var message = "<html><body><p>Hello!</p></body></html>";
            var writer = new StreamWriter(ssl, new UTF8Encoding(false));
            writer.WriteLine("HTTP/1.0 200 OK");
            writer.WriteLine("Content-Type: text/html; charset=utf-8");
            writer.WriteLine("Content-Length: " + message.Length);
            writer.WriteLine();
            writer.WriteLine(message);
            writer.Flush();
            ssl.Flush();
        }

        bool ValidateCertificate(object sender, X509Certificate clientCertificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return true;
        }

        static void EnsureCertificateIsValidForListening(X509Certificate2 certificate)
        {
            if (certificate == null) throw new Exception("No certificate was provided.");

            if (!certificate.HasPrivateKey)
            {
                throw new Exception("The X509 certificate provided does not have a private key, and so it cannot be used for listening.");
            }
        }

        public void Dispose()
        {
            isStopped = true;
            listener.Stop();
        }
    }
}