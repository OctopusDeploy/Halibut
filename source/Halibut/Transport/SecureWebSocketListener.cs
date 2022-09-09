using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
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
        readonly ExchangeProtocolBuilder exchangeProtocolBuilder;
        readonly Predicate<string> verifyClientThumbprint;
        readonly Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        readonly Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly ExchangeActionAsync exchangeAction;
        ILog log;
        HttpListener listener;

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, ExchangeProtocolBuilder exchangeProtocolBuilder, ExchangeActionAsync exchangeAction, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
            : this(endPoint, serverCertificate, exchangeProtocolBuilder, exchangeAction, verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, () => new Dictionary<string, string>())
        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, ExchangeProtocolBuilder exchangeProtocolBuilder, ExchangeActionAsync exchangeAction, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders)
            : this(endPoint, serverCertificate, exchangeProtocolBuilder, exchangeAction, verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, getFriendlyHtmlPageHeaders, (clientName, thumbprint) => UnauthorizedClientConnectResponse.BlockConnection)
        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, ExchangeProtocolBuilder exchangeProtocolBuilder, ExchangeActionAsync exchangeAction, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders,
            Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect)
        {
            if (!endPoint.EndsWith("/"))
                endPoint += "/";

            this.endPoint = endPoint;
            this.serverCertificate = serverCertificate;
            this.exchangeProtocolBuilder = exchangeProtocolBuilder;
            this.exchangeAction = exchangeAction;
            this.verifyClientThumbprint = verifyClientThumbprint;
            this.unauthorizedClientConnect = unauthorizedClientConnect;
            this.logFactory = logFactory;
            this.getFriendlyHtmlPageContent = getFriendlyHtmlPageContent;
            this.getFriendlyHtmlPageHeaders = getFriendlyHtmlPageHeaders;
            EnsureCertificateIsValidForListening(serverCertificate);
        }

        public void Start()
        {
            log = logFactory.ForPrefix(endPoint);

            if (!endPoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Web socket listen prefixes must start with https://");

            listener = new HttpListener();
            listener.Prefixes.Add(endPoint);

            if (!SecureListener.IsWindows())
            {
                // https://github.com/dotnet/runtime/issues/27391#issuecomment-431933662
                log.Write(EventType.Diagnostic, "Setting Cert for " + endPoint);

                var portMatch = Regex.Match(endPoint, @":([0-9]+)");
                var port = portMatch.Success ? int.Parse(portMatch.Groups[1].Value) : 443;
                
                var hostMatch = Regex.Match("https://+:8433/Halibut/", @"https://([^/:]+)", RegexOptions.IgnoreCase);
                var host = hostMatch.Groups[1].Value;
                
                var epl = Type.GetType("System.Net.HttpEndPointManager, System.Net.HttpListener")!
                    .GetMethod("GetEPListener", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, new object[] { host, port, listener, true });
                Type.GetType("System.Net.HttpEndPointListener, System.Net.HttpListener")!
                    .GetField("_cert", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(epl, serverCertificate);

                log.Write(EventType.Diagnostic, "Cert set");
            }

            listener.TimeoutManager.IdleConnection = HalibutLimits.TcpClientReceiveTimeout;
            listener.Start();

            log.Write(EventType.ListenerStarted, "WebSockert Listener started");
            Task.Run(async () => await Accept().ConfigureAwait(false)).ConfigureAwait(false);
        }

        async Task Accept()
        {
            log.Write(EventType.Diagnostic, "Accept started");

            using (cts.Token.Register(listener.Stop))
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        log.Write(EventType.Diagnostic, "GetContext");

                        var context = await listener.GetContextAsync().ConfigureAwait(false);
                        log.Write(EventType.Diagnostic, "Got Connection");

                        if (context.Request.IsWebSocketRequest)
                        {
                            log.Write(EventType.Diagnostic, "WebSocket!");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            Task.Run(async () => await HandleClient(context).ConfigureAwait(false)).ConfigureAwait(false);
#pragma warning restore CS4014
                        }
                        else
                        {
                            log.Write(EventType.Diagnostic, "Not a WebSocket");

                            log.Write(EventType.Error, $"Rejected connection from {context.Request.RemoteEndPoint} as it is not Web Socket request");
                            SendFriendlyHtmlPage(context.Response);
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

                var authorized = await Authorize(listenerContext, clientName).ConfigureAwait(false);

                if (authorized)
                {
                    // Delegate the open stream to the protocol handler - we no longer own the stream lifetime
                    await ExchangeMessages(webSocketStream).ConfigureAwait(false);

                    // Mark the stream as delegated once everything has succeeded
                    keepConnection = true;
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
            foreach (var header in headers)
                response.AddHeader(header.Key, header.Value);

            // This could fail if the client terminates the connection and we attempt to write to it
            // Disposing the StreamWriter will close the stream - it owns the stream
            using (var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false)) { NewLine = "\r\n" })
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

            return exchangeAction(exchangeProtocolBuilder(stream, log));
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