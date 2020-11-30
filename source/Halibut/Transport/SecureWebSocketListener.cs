using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureWebSocketListener : IDisposable
    {
        readonly string endPoint;
        readonly X509Certificate2 serverCertificate;
        readonly Func<MessageExchangeProtocol, Task> protocolHandler;
        readonly Predicate<string> verifyClientThumbprint;
        readonly Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        readonly Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        ILog log;
        HttpListener listener;

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, Action<MessageExchangeProtocol> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
            : this(endPoint, serverCertificate, h => Task.Run(() => protocolHandler(h)), verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, () => new Dictionary<string, string>())

        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, Action<MessageExchangeProtocol> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders)
            : this(endPoint, serverCertificate, h => Task.Run(() => protocolHandler(h)), verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, getFriendlyHtmlPageHeaders)

        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, Func<MessageExchangeProtocol, Task> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
            : this(endPoint, serverCertificate, h => Task.Run(() => protocolHandler(h)), verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, () => new Dictionary<string, string>())
        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, Func<MessageExchangeProtocol, Task> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders)
            : this(endPoint, serverCertificate, protocolHandler, verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, getFriendlyHtmlPageHeaders, (clientName, thumbprint) => UnauthorizedClientConnectResponse.BlockConnection)
        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, Func<MessageExchangeProtocol, Task> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders, Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect)
        {
            if (!endPoint.EndsWith("/"))
                endPoint += "/";

            this.endPoint = endPoint;
            this.serverCertificate = serverCertificate;
            this.protocolHandler = protocolHandler;
            this.verifyClientThumbprint = verifyClientThumbprint;
            this.unauthorizedClientConnect = unauthorizedClientConnect;
            this.logFactory = logFactory;
            this.getFriendlyHtmlPageContent = getFriendlyHtmlPageContent;
            this.getFriendlyHtmlPageHeaders = getFriendlyHtmlPageHeaders;
            EnsureCertificateIsValidForListening(serverCertificate);
        }

        public void Start()
        {
            if (!endPoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Web socket listen prefixes must start with https://");

            listener = new HttpListener();
            listener.Prefixes.Add(endPoint);
            listener.TimeoutManager.IdleConnection = HalibutLimits.TcpClientReceiveTimeout;
            listener.Start();

            log = logFactory.ForPrefix(endPoint);
            log.Write(EventType.ListenerStarted, "Listener started");
            Task.Run(async () => await Accept().ConfigureAwait(false));
        }

        async Task Accept()
        {
            using (cts.Token.Register(listener.Stop))
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var context = await listener.GetContextAsync().ConfigureAwait(false);

                        if (context.Request.IsWebSocketRequest)
                        {
                            await HandleClient(context).ConfigureAwait(false);
                        }
                        else
                        {
                            log.Write(EventType.Error, $"Rejected connection from {context.Request.RemoteEndPoint} as it is not Web Socket request");
                            SendFriendlyHtmlPage(context.Response);
                            context.Response.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        if(!cts.IsCancellationRequested)
                            log.WriteException(EventType.Error, "Error accepting Web Socket client", ex);
                    }
                }
            }
        }

        async Task HandleClient(HttpListenerContext context)
        {
            try
            {
                log.Write(EventType.ListenerAcceptedClient, "Accepted Web Socket client: {0}", context.Request.RemoteEndPoint);
                await ExecuteRequest(context).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Error initializing TCP client", ex);
            }
        }

        async Task ExecuteRequest(HttpListenerContext listenerContext)
        {
            // By default we will close the stream to cater for failure scenarios
            var keepConnection = false;

            var clientName = listenerContext.Request.RemoteEndPoint;

            WebSocketStream webSocketStream = null;
            try
            {
                var webSocketContext = await listenerContext.AcceptWebSocketAsync("Octopus").ConfigureAwait(false);
                webSocketStream = new WebSocketStream(webSocketContext.WebSocket);

                var req = await webSocketStream.ReadTextMessage().ConfigureAwait(false); // Initial message
                if (string.IsNullOrEmpty(req))
                {
                    log.Write(EventType.Diagnostic, "Ignoring empty request");
                    return;
                }

                if (req.Substring(0, 2) != "MX")
                {
                    log.Write(EventType.Diagnostic, "Appears to be a web browser, sending friendly HTML response");
                    return;
                }

                if (await Authorize(listenerContext, clientName).ConfigureAwait(false))
                {
                    // Delegate the open stream to the protocol handler - we no longer own the stream lifetime
                    await ExchangeMessages(webSocketStream).ConfigureAwait(false);

                    // Mark the stream as delegated once everything has succeeded
                    keepConnection = true;
                }
            }
            catch (TaskCanceledException)
            {
                if(!cts.Token.IsCancellationRequested)
                    log.Write(EventType.Error, "A timeout occurred while receiving data");
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Unhandled error when handling request from client: {0}", ex, clientName);
            }
            finally
            {
                if (!keepConnection)
                {
                    // Closing an already closed stream or client is safe, better not to leak
                    webSocketStream?.Dispose();
                    listenerContext.Response.Close();
                }
            }
        }

      
        void SendFriendlyHtmlPage(HttpListenerResponse response)
        {
            var message = getFriendlyHtmlPageContent();
            var headers = getFriendlyHtmlPageHeaders();
            response.AddHeader("Content-Type", "text/html; charset=utf-8");
            foreach(var header in headers)
                response.AddHeader(header.Key, header.Value);

            // This could fail if the client terminates the connection and we attempt to write to it
            // Disposing the StreamWriter will close the stream - it owns the stream
            using (var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false)){ NewLine = "\r\n" })
            {
                writer.WriteLine(message);
                writer.Flush();
            }
        }

        async Task<bool> Authorize(HttpListenerContext context, EndPoint clientName)
        {
            log.Write(EventType.Diagnostic, "Begin authorization");
            var certificate = await context.Request.GetClientCertificateAsync().ConfigureAwait(false);
            if (certificate == null)
            {
                log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but did not present a client certificate", clientName);
                return false;
            }

            var thumbprint = new X509Certificate2(certificate.Export(X509ContentType.Cert)).Thumbprint;
            var isAuthorized = verifyClientThumbprint(thumbprint);

            if (!isAuthorized)
            {
                log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but it presented a client certificate with the thumbprint '{1}' which is not in the list of thumbprints that we trust", clientName, thumbprint);
                var response = unauthorizedClientConnect(clientName.ToString(), thumbprint);
                if (response == UnauthorizedClientConnectResponse.BlockConnection)
                    return false;
            }

            log.Write(EventType.Security, "Client at {0} authenticated as {1}", clientName, thumbprint);
            return true;
        }

        Task ExchangeMessages(WebSocketStream stream)
        {
            log.Write(EventType.Diagnostic, "Begin message exchange");

            return protocolHandler(new MessageExchangeProtocol(stream, log));
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


        public void Dispose()
        {
            cts.Cancel();
            log.Write(EventType.ListenerStopped, "Listener stopped");
        }
    }
}
