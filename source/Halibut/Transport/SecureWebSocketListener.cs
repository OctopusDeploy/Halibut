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
using Halibut.Util;

namespace Halibut.Transport
{
    public class SecureWebSocketListener : IDisposable
    {
        readonly string endPoint;
        readonly ExchangeProtocolBuilder exchangeProtocolBuilder;
        readonly Predicate<string> verifyClientThumbprint;
        readonly Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        readonly Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders;
        readonly CancellationTokenSource cts = new();
        readonly ExchangeActionAsync exchangeAction;
        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        ILog log;
        HttpListener listener;

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, ExchangeProtocolBuilder exchangeProtocolBuilder, ExchangeActionAsync exchangeAction, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, AsyncHalibutFeature asyncHalibutFeature, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
            : this(endPoint, serverCertificate, exchangeProtocolBuilder, exchangeAction, verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, () => new Dictionary<string, string>(), asyncHalibutFeature, halibutTimeoutsAndLimits)
        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, ExchangeProtocolBuilder exchangeProtocolBuilder, ExchangeActionAsync exchangeAction, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders, AsyncHalibutFeature asyncHalibutFeature, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
            : this(endPoint, serverCertificate, exchangeProtocolBuilder, exchangeAction, verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, getFriendlyHtmlPageHeaders, (clientName, thumbprint) => UnauthorizedClientConnectResponse.BlockConnection, asyncHalibutFeature, halibutTimeoutsAndLimits)
        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, ExchangeProtocolBuilder exchangeProtocolBuilder, ExchangeActionAsync exchangeAction, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders, Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect, AsyncHalibutFeature asyncHalibutFeature, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            if (!endPoint.EndsWith("/"))
                endPoint += "/";

            this.endPoint = endPoint;
            this.exchangeProtocolBuilder = exchangeProtocolBuilder;
            this.exchangeAction = exchangeAction;
            this.verifyClientThumbprint = verifyClientThumbprint;
            this.unauthorizedClientConnect = unauthorizedClientConnect;
            this.logFactory = logFactory;
            this.getFriendlyHtmlPageContent = getFriendlyHtmlPageContent;
            this.getFriendlyHtmlPageHeaders = getFriendlyHtmlPageHeaders;
            this.asyncHalibutFeature = asyncHalibutFeature;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            EnsureCertificateIsValidForListening(serverCertificate);
        }

        public void Start()
        {
            if (!endPoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Web socket listen prefixes must start with https://");

            listener = new HttpListener();
            listener.Prefixes.Add(endPoint);
            listener.TimeoutManager.IdleConnection = asyncHalibutFeature.IsEnabled()
                ? halibutTimeoutsAndLimits.TcpClientReceiveTimeout 
#pragma warning disable CS0612
                : HalibutLimits.TcpClientReceiveTimeout;
#pragma warning restore CS0612
            listener.Start();

            log = logFactory.ForPrefix(endPoint);
            log.Write(EventType.ListenerStarted, "Listener started");
            Task.Run(async () => await Accept().ConfigureAwait(false)).ConfigureAwait(false);
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
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            Task.Run(async () => await HandleClient(context).ConfigureAwait(false)).ConfigureAwait(false);
#pragma warning restore CS4014
                        }
                        else
                        {
                            log.Write(EventType.Error, $"Rejected connection from {context.Request.RemoteEndPoint} as it is not Web Socket request");
                            await SendFriendlyHtmlPage(context.Response);
                            context.Response.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!cts.IsCancellationRequested)
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
            var clientName = listenerContext.Request.RemoteEndPoint;

            WebSocketStream webSocketStream = null;
            try
            {
                var webSocketContext = await listenerContext.AcceptWebSocketAsync("Octopus").ConfigureAwait(false);
                webSocketStream = new WebSocketStream(webSocketContext.WebSocket);

                string req;
                if (asyncHalibutFeature.IsEnabled())
                {
                    req = await webSocketStream.ReadTextMessage(cts.Token).ConfigureAwait(false);
                }
                else
                {
#pragma warning disable CS0612 // Type or member is obsolete
                    req = await webSocketStream.ReadTextMessageSynchronouslyAsync().ConfigureAwait(false); // Initial message
#pragma warning restore CS0612 // Type or member is obsolete
                }

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

                var authorized = await Authorize(listenerContext, clientName).ConfigureAwait(false);

                if (authorized)
                {
                    // Delegate the open stream to the protocol handler - we no longer own the stream lifetime
                    await ExchangeMessages(webSocketStream).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                if (!cts.Token.IsCancellationRequested)
                    log.Write(EventType.Error, "A timeout occurred while receiving data");
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Unhandled error when handling request from client: {0}", ex, clientName);
            }
            finally
            {
                // Closing an already closed stream or client is safe, better not to leak
                webSocketStream?.Dispose();
                listenerContext.Response.Close();
            }
        }

        async Task SendFriendlyHtmlPage(HttpListenerResponse response)
        {
            var message = getFriendlyHtmlPageContent();
            var headers = getFriendlyHtmlPageHeaders();
            response.AddHeader("Content-Type", "text/html; charset=utf-8");
            foreach (var header in headers)
                response.AddHeader(header.Key, header.Value);

            // This could fail if the client terminates the connection and we attempt to write to it
            // Disposing the StreamWriter will close the stream - it owns the stream
            using (var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false)) { NewLine = "\r\n" })
            {
                if (asyncHalibutFeature.IsEnabled())
                {
                    await writer.WriteLineAsync(message);
                    await writer.FlushAsync();
                }
                else
                {
                    writer.WriteLine(message);
                    writer.Flush();
                }
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

            var thumbprint = new X509Certificate2(certificate.Export(X509ContentType.Cert), (string)null!).Thumbprint;
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

            return exchangeAction(exchangeProtocolBuilder(stream, log), cts.Token);
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
            cts.Dispose();
            log.Write(EventType.ListenerStopped, "Listener stopped");
        }
    }
}
