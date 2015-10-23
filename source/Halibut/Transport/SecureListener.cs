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
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureListener
    {
        readonly IPEndPoint endPoint;
        readonly X509Certificate2 serverCertificate;
        readonly Action<MessageExchangeProtocol> protocolHandler;
        readonly Predicate<string> verifyClientThumbprint;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        ILog log;
        TcpListener listener;
        bool isStopped;

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, Action<MessageExchangeProtocol> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
        {
            this.endPoint = endPoint;
            this.serverCertificate = serverCertificate;
            this.protocolHandler = protocolHandler;
            this.verifyClientThumbprint = verifyClientThumbprint;
            this.logFactory = logFactory;
            this.getFriendlyHtmlPageContent = getFriendlyHtmlPageContent;

            EnsureCertificateIsValidForListening(serverCertificate);
        }

        public int Start()
        {
            listener = new TcpListener(endPoint);
            listener.Server.DualMode = (endPoint.Address.AddressFamily == AddressFamily.InterNetworkV6);
            listener.Start();
            log = logFactory.ForEndpoint(new Uri("listen://" + listener.LocalEndpoint));
            log.Write(EventType.ListenerStarted, "Listener started");
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
                    if (isStopped)
                        return;

                    var client = listener.EndAcceptTcpClient(r);
                    if (isStopped)
                        return;

                    client.SendTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalMilliseconds;
                    client.ReceiveTimeout = (int)HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds;

                    log.Write(EventType.ListenerAcceptedClient, "Accepted TCP client: {0}", client.Client.RemoteEndPoint);

                    var task = new Task(() => ExecuteRequest(client));
                    task.Start();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Error accepting TCP client", ex);
                }

                Accept();
            }, null);
        }

        void ExecuteRequest(TcpClient client)
        {
            // By default we will close the stream to cater for failure scenarios
            var keepStreamOpen = false;

            var clientName = client.Client.RemoteEndPoint;
            var stream = client.GetStream();

            using (var ssl = new SslStream(stream, true, AcceptAnySslCertificate))
            {
                try
                {
                    log.Write(EventType.Security, "Performing TLS server handshake");
                    ssl.AuthenticateAsServer(serverCertificate, true, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);

                    log.Write(EventType.Security, "Secure connection established, client is not yet authenticated, client connected with {0}", ssl.SslProtocol.ToString());

                    var req = ReadInitialRequest(ssl);
                    if (string.IsNullOrEmpty(req))
                    {
                        log.Write(EventType.Diagnostic, "Ignoring empty request");
                        return;
                    }

                    if (req.Substring(0, 2) != "MX")
                    {
                        log.Write(EventType.Diagnostic, "Appears to be a web browser, sending friendly HTML response");
                        SendFriendlyHtmlPage(ssl);
                        return;
                    }

                    if (Authorize(ssl, clientName))
                    {
                        // Delegate the open stream to the protocol handler - we no longer own the stream lifetime
                        ExchangeMessages(ssl);

                        // Mark the stream as delegated once everything has succeeded
                        keepStreamOpen = true;
                    }
                }
                catch (AuthenticationException ex)
                {
                    log.WriteException(EventType.ClientDenied, "Client failed authentication: {0}", ex, clientName);
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Unhandled error when handling request from client: {0}", ex, clientName);
                }
                finally
                {
                    if (!keepStreamOpen)
                    {
                        // Closing an already closed stream or client is safe, better not to leak
                        stream.Close();
                        client.Close();
                    }
                }
            }
        }

        void SendFriendlyHtmlPage(Stream stream)
        {
            var message = getFriendlyHtmlPageContent();

            // This could fail if the client terminates the connection and we attempt to write to it
            // Disposing the StreamWriter will close the stream - it owns the stream
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.WriteLine("HTTP/1.0 200 OK");
                writer.WriteLine("Content-Type: text/html; charset=utf-8");
                writer.WriteLine("Content-Length: " + message.Length);
                writer.WriteLine();
                writer.WriteLine(message);
                writer.WriteLine();
                writer.Flush();
                stream.Flush();
            }
        }

        bool Authorize(SslStream stream, EndPoint clientName)
        {
            log.Write(EventType.Diagnostic, "Begin authorization");

            if (stream.RemoteCertificate == null)
            {
                log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but did not present a client certificate", clientName);
                return false;
            }

            var thumbprint = new X509Certificate2(stream.RemoteCertificate).Thumbprint;
            var isAuthorized = verifyClientThumbprint(thumbprint);

            if (!isAuthorized)
            {
                log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but it presented a client certificate with the thumbprint '{1}' which is not in the list of thumbprints that we trust", clientName, thumbprint);
                return false;
            }

            log.Write(EventType.Security, "Client at {0} authenticated as {1}", clientName, thumbprint);
            return true;
        }

        void ExchangeMessages(SslStream stream)
        {
            log.Write(EventType.Diagnostic, "Begin message exchange");

            protocolHandler(new MessageExchangeProtocol(stream, log));
        }

        bool AcceptAnySslCertificate(object sender, X509Certificate clientCertificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return true;
        }

        // ReSharper disable once UnusedParameter.Local
        static void EnsureCertificateIsValidForListening(X509Certificate2 certificate)
        {
            if (certificate == null) throw new Exception("No certificate was provided.");

            if (!certificate.HasPrivateKey)
            {
                throw new Exception("The X509 certificate provided does not have a private key, and so it cannot be used for listening.");
            }
        }

        string ReadInitialRequest(Stream stream)
        {
            var builder = new StringBuilder();
            var lastWasNewline = false;
            while (builder.Length < 20000)
            {
                var b = stream.ReadByte();
                if (b == -1) return builder.ToString();

                var c = (char) b;
                if (c == '\r')
                {
                    continue;
                }
                else if (c == '\n')
                {
                    builder.AppendLine();

                    if (lastWasNewline)
                        break;

                    lastWasNewline = true;
                }
                else
                {
                    lastWasNewline = false;
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        public void Dispose()
        {
            isStopped = true;
            listener.Stop();
            log.Write(EventType.ListenerStopped, "Listener stopped");
        }
    }
}