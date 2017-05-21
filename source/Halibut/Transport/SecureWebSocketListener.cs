#if HAS_SERVICE_POINT_MANAGER
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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
    public class SecureWebSocketListener : IDisposable, Stoppable
    {
        readonly string endPoint;
        readonly Func<MessageExchangeProtocol, Task> protocolHandler;
        readonly Predicate<string> verifyClientThumbprint;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        CancellationTokenSource cts = new CancellationTokenSource();
        ILog log;
        HttpListener listener;
        readonly ConcurrentDictionary<Task, Task> runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
        Task acceptPumpTask;

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, Action<MessageExchangeProtocol> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
            : this(endPoint, serverCertificate, h => Task.Run(() => protocolHandler(h)), verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent)

        {
        }

        public SecureWebSocketListener(string endPoint, X509Certificate2 serverCertificate, Func<MessageExchangeProtocol, Task> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
        {
            if (!endPoint.EndsWith("/"))
                endPoint += "/";

            this.endPoint = endPoint;
            this.protocolHandler = protocolHandler;
            this.verifyClientThumbprint = verifyClientThumbprint;
            this.logFactory = logFactory;
            this.getFriendlyHtmlPageContent = getFriendlyHtmlPageContent;
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
            acceptPumpTask = Task.Run(Accept, CancellationToken.None);
        }

        public async Task Stop()
        {
            cts.Cancel();
            listener.Stop();

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var allTasks = runningReceiveTasks.Values.Concat(new[]
            {
                acceptPumpTask
            });
            var finishedTask = await Task.WhenAny(Task.WhenAll(allTasks), timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                log.Write(EventType.Error, "The accept pump failed to stop within the time allowed(30s)");
            }

            runningReceiveTasks.Clear();

            log.Write(EventType.ListenerStopped, "Listener stopped");
        }

        async Task Accept()
        {
            while (!cts.IsCancellationRequested)
            {
                using (cts.Token.Register(listener.Stop))
                {
                    try
                    {
                        var context = await listener.GetContextAsync().ConfigureAwait(false);

                        if (context.Request.IsWebSocketRequest)
                        {
                            var receiveTask = HandleClient(context);

                            runningReceiveTasks.TryAdd(receiveTask, receiveTask);

                            receiveTask.ContinueWith(t =>
                                {
                                    Task toBeRemoved;
                                    runningReceiveTasks.TryRemove(receiveTask, out toBeRemoved);
                                }, TaskContinuationOptions.ExecuteSynchronously)
                                .Ignore();
                        }
                        else
                        {
                            log.Write(EventType.Error, $"Rejected connection from {context.Request.RemoteEndPoint} as it is not Web Socket request");
                            await SendFriendlyHtmlPage(context.Response).ConfigureAwait(false);
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

        async Task SendFriendlyHtmlPage(HttpListenerResponse response)
        {
            var message = getFriendlyHtmlPageContent();
            response.AddHeader("Content-Type", "text/html; charset=utf-8");
            // This could fail if the client terminates the connection and we attempt to write to it
            // Disposing the StreamWriter will close the stream - it owns the stream
            using (var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false)))
            {
                await writer.WriteLineAsync(message).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }

        async Task<bool> Authorize(HttpListenerContext context, EndPoint clientName)
        {
            log.Write(EventType.Diagnostic, "Begin authorization");
            var certificate = await context.Request.GetClientCertificateAsync().ConfigureAwait(false);
            if (certificate == null)
            {
                log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but did not present a client certificate", clientName);
                return true;
            }

            var thumbprint = new X509Certificate2(certificate.Export(X509ContentType.Cert)).Thumbprint;
            var isAuthorized = verifyClientThumbprint(thumbprint);

            if (!isAuthorized)
            {
                log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but it presented a client certificate with the thumbprint '{1}' which is not in the list of thumbprints that we trust", clientName, thumbprint);
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
            // Injected by Fody.Janitor
        }
    }
}
#endif