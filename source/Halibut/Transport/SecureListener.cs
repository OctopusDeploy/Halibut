using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;
using Halibut.Util;

namespace Halibut.Transport
{
    [Flags]
    enum HANDLE_FLAGS : uint
    {
        None = 0,
        INHERIT = 1,
        PROTECT_FROM_CLOSE = 2
    }

    public class SecureListener : IAsyncDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetHandleInformation(IntPtr hObject, HANDLE_FLAGS dwMask, HANDLE_FLAGS dwFlags);

        readonly IPEndPoint endPoint;
        readonly X509Certificate2 serverCertificate;
        readonly ExchangeProtocolBuilder exchangeProtocolBuilder;
        readonly Predicate<string> verifyClientThumbprint;
        readonly Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        readonly Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;
        readonly TcpClientManager tcpClientManager = new();
        readonly ExchangeActionAsync exchangeAction;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly IStreamFactory streamFactory;
        readonly IConnectionsObserver connectionsObserver;
        readonly ISecureConnectionObserver secureConnectionObserver;
        ILog log;
        TcpListener listener;
        Thread? backgroundThread;
        Task? backgroundTask;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public SecureListener(
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            IPEndPoint endPoint,
            X509Certificate2 serverCertificate,
            ExchangeProtocolBuilder exchangeProtocolBuilder,
            ExchangeActionAsync exchangeAction,
            Predicate<string> verifyClientThumbprint,
            ILogFactory logFactory,
            Func<string> getFriendlyHtmlPageContent,
            Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders,
            Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits,
            IStreamFactory streamFactory,
            IConnectionsObserver connectionsObserver,
            ISecureConnectionObserver secureConnectionObserver
        )
        {
            this.endPoint = endPoint;
            this.serverCertificate = serverCertificate;
            this.exchangeProtocolBuilder = exchangeProtocolBuilder;
            this.exchangeAction = exchangeAction;
            this.verifyClientThumbprint = verifyClientThumbprint;
            this.unauthorizedClientConnect = unauthorizedClientConnect;
            this.logFactory = logFactory;
            this.getFriendlyHtmlPageContent = getFriendlyHtmlPageContent;
            this.getFriendlyHtmlPageHeaders = getFriendlyHtmlPageHeaders;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.streamFactory = streamFactory;
            this.connectionsObserver = connectionsObserver;
            this.secureConnectionObserver = secureConnectionObserver;
            this.cts = new CancellationTokenSource();
            this.cancellationToken = cts.Token;

            EnsureCertificateIsValidForListening(serverCertificate);
        }

        public int Start()
        {
            listener = new TcpListener(endPoint);
            if (endPoint.Address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                listener.Server.DualMode = true;
            }

            listener.Start();

            if (IsWindows())
            {
                // set socket handle as not inherited so that when tentacle runs powershell
                // with System.Diagnostics.Process those scripts don't lock the socket
                SetHandleInformation(listener.Server.Handle, HANDLE_FLAGS.INHERIT, HANDLE_FLAGS.None);
            }

            log = logFactory.ForEndpoint(new Uri("listen://" + listener.LocalEndpoint));
            log.Write(EventType.ListenerStarted, "Listener started");

            if (halibutTimeoutsAndLimits.UseAsyncListener)
            {
                backgroundTask = Task.Run(() => AcceptAsyncIgnoringExceptions(cancellationToken));
            }
            else
            {
#pragma warning disable CS0612 // Type or member is obsolete
                backgroundThread = new Thread(Accept)
#pragma warning restore CS0612 // Type or member is obsolete
                {
                    Name = "Accept connections on " + listener.LocalEndpoint
                };
                backgroundThread.IsBackground = true;
                backgroundThread.Start();
            }

            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        // TODO - ASYNC ME UP - Async Version Needed
        public void Disconnect(string thumbprint)
        {
            tcpClientManager.Disconnect(thumbprint);
        }

        [Obsolete]
        void Accept()
        {
            // See: https://github.com/OctopusDeploy/Issues/issues/6035
            // See: https://github.com/dotnet/corefx/issues/26034

            void WaitForPendingConnectionOrCancellation()
            {
                SpinWait.SpinUntil(() => cts.IsCancellationRequested || listener.Pending());
            }

            const int errorThreshold = 3;

            using (IsWindows() ? cancellationToken.Register(listener.Stop) : (IDisposable)null!)
            {
                var numberOfFailedAttemptsInRow = 0;
                while (!cts.IsCancellationRequested)
                {
                    TcpClient? client = null;
                    try
                    {
                        if (!IsWindows())
                        {
                            WaitForPendingConnectionOrCancellation();

                            if (cts.IsCancellationRequested)
                            {
                                return;
                            }
                        }

                        client = listener.AcceptTcpClient();

                        Task.Run(async () => await HandleClient(client).ConfigureAwait(false)).ConfigureAwait(false);
                        numberOfFailedAttemptsInRow = 0;
                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                        // Happens on shutdown
                    }
                    catch (Exception ex)
                    {
                        numberOfFailedAttemptsInRow++;
                        log.WriteException(EventType.ErrorInInitialisation, "Error accepting TCP client: {0}", ex, client!.GetRemoteEndpointString());
                        // Slow down the logs in case an exception is immediately encountered after X failed AcceptTcpClient calls
                        if (numberOfFailedAttemptsInRow >= errorThreshold)
                        {
                            var millisecondsTimeout = Math.Max(0, Math.Min(numberOfFailedAttemptsInRow - errorThreshold, 100)) * 10;
                            log.Write(
                                EventType.ErrorInInitialisation,
                                $"Accepting a connection has failed {numberOfFailedAttemptsInRow} times in a row. Waiting {millisecondsTimeout}ms before attempting to accept another connection. For a detailed troubleshooting guide go to https://g.octopushq.com/TentacleTroubleshooting"
                            );
                            Thread.Sleep(millisecondsTimeout);
                        }
                    }
                }
            }
        }

        async Task AcceptAsyncIgnoringExceptions(CancellationToken cancellationToken)
        {
            try
            {
                await AcceptAsync(cancellationToken);
            }
            catch (Exception)
            {
                // This matches what we did before, in theory this will never happen.
            }
        }

        async Task AcceptAsync(CancellationToken cancellationToken)
        {
            const int errorThreshold = 3;

            // Don't call listener.Stop until we sure we are not doing an Accept
            // See: https://github.com/OctopusDeploy/Issues/issues/6035
            // See: https://github.com/dotnet/corefx/issues/26034
            using (IsWindows() ? cancellationToken.Register(listener.Stop) : (IDisposable)null!)
            {
                var numberOfFailedAttemptsInRow = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient? client = null;
                    try
                    {
#if !NETFRAMEWORK
                        client = await listener.AcceptTcpClientAsync(this.cancellationToken);
#else
                        // This only works because in the using we stop the listener which should work on windows
                        client = await listener.AcceptTcpClientAsync();
#endif
                        client.NoDelay = halibutTimeoutsAndLimits.TcpNoDelay;
                        var _ = Task.Run(async () => await HandleClient(client).ConfigureAwait(false)).ConfigureAwait(false);
                        numberOfFailedAttemptsInRow = 0;
                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted)
                    {
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                        // Happens on shutdown
                    }
                    catch (Exception ex)
                    {
                        numberOfFailedAttemptsInRow++;
                        log.WriteException(EventType.ErrorInInitialisation, "Error accepting TCP client: {0}", ex, client!.GetRemoteEndpointString());
                        // Slow down the logs in case an exception is immediately encountered after X failed AcceptTcpClient calls
                        if (numberOfFailedAttemptsInRow >= errorThreshold)
                        {
                            var millisecondsTimeout = Math.Max(0, Math.Min(numberOfFailedAttemptsInRow - errorThreshold, 100)) * 10;
                            log.Write(
                                EventType.ErrorInInitialisation,
                                $"Accepting a connection has failed {numberOfFailedAttemptsInRow} times in a row. Waiting {millisecondsTimeout}ms before attempting to accept another connection. For a detailed troubleshooting guide go to https://g.octopushq.com/TentacleTroubleshooting"
                            );
                            await Task.Delay(millisecondsTimeout, cancellationToken);
                        }
                    }
                }
            }
        }

        bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        async Task HandleClient(TcpClient client)
        {
            try
            {
                client.SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientTimeout.SendTimeout.TotalMilliseconds;
                client.ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout.TotalMilliseconds;

                log.Write(EventType.ListenerAcceptedClient, "Accepted TCP client: {0}", client.GetRemoteEndpointString());
                await ExecuteRequest(client).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.ErrorInInitialisation, "Error initializing TCP client: {0}", ex, client.GetRemoteEndpointString());
            }
        }

        async Task ExecuteRequest(TcpClient client)
        {
            var connectionAuthorizedAndObserved = false;

            var clientName = client.GetRemoteEndpointString();

            var stream = streamFactory.CreateStream(client);
            client.ConfigureTcpOptions(halibutTimeoutsAndLimits);

            var errorEventType = EventType.ErrorInInitialisation;
            try
            {
                var ssl = new SslStream(stream, true, AcceptAnySslCertificate);
                await using (Try.CatchingErrorOnDisposal(ssl, ex => log.WriteException(EventType.Diagnostic, "Could not dispose SSL stream", ex)))
                {
                    log.Write(EventType.SecurityNegotiation, "Performing TLS server handshake");

                    await ssl
                        .AuthenticateAsServerAsync(
                            serverCertificate,
                            true,
                            SslConfiguration.SupportedProtocols,
                            false)
                        .ConfigureAwait(false);

                    log.Write(EventType.SecurityNegotiation, "Secure connection established, client is not yet authenticated, client connected with {0}", ssl.SslProtocol.ToString());

                    var req = await ReadInitialRequest(ssl);
                    if (string.IsNullOrEmpty(req))
                    {
                        log.Write(EventType.Diagnostic, "Ignoring empty request");
                        return;
                    }

                    if (req.Substring(0, 2) != "MX")
                    {
                        log.Write(EventType.Diagnostic, "Appears to be a web browser, sending friendly HTML response");
                        await SendFriendlyHtmlPage(ssl);
                        return;
                    }

                    var thumbprint = GetThumbprint(ssl);
                    if (thumbprint == null)
                    {
                        log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but did not present a client certificate", clientName);
                        return;
                    }

                    if (Authorize(thumbprint, clientName))
                    {
                        connectionAuthorizedAndObserved = true;
                        connectionsObserver.ConnectionAccepted(true);
                        secureConnectionObserver.SecureConnectionEstablished(SecureConnectionInfo.CreateIncoming(ssl.SslProtocol));
                        tcpClientManager.AddActiveClient(thumbprint, client);
                        errorEventType = EventType.Error;
                        await ExchangeMessages(ssl).ConfigureAwait(false);
                    }
                }
            }
            catch (AuthenticationException ex)
            {
                log.WriteException(EventType.ClientDenied, "Client failed authentication: {0}", ex, clientName);
            }
            catch (ActiveTcpConnectionsExceededException ex)
            {
                log.Write(EventType.ErrorInInitialisation, "A polling client at {0} has exceeded the maximum number of active connections ({1}) for subscription {2}. New connections will be rejected", clientName, halibutTimeoutsAndLimits.MaximumActiveTcpConnectionsPerPollingSubscription, ex.SubscriptionId.ToString());
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                log.WriteException(errorEventType, "Socket IO exception: {0}", ex.InnerException, clientName);
            }
            catch (IOException ex) when (ex.InnerException is ObjectDisposedException)
            {
                log.WriteException(EventType.ListenerStopped, "Socket IO exception: {0}", ex.InnerException, clientName);
            }
            catch (SocketException ex)
            {
                log.WriteException(errorEventType, "Socket exception: {0}", ex, clientName);
            }
            catch (Exception ex)
            {
                log.WriteException(errorEventType, "Unhandled error when handling request from client: {0}", ex, clientName);
            }
            finally
            {
                if (!connectionAuthorizedAndObserved)
                {
                    connectionsObserver.ConnectionAccepted(false);
                }

                try
                {
                    SafelyRemoveClientFromTcpClientManager(client, clientName);
                    await SafelyCloseStreamAsync(stream, clientName);
                    client.CloseImmediately(ex => log.Write(errorEventType, "Failed to close TcpClient for {0}. This may result in a memory leak. {1}", clientName, ex.Message));
                }
                finally
                {
                    connectionsObserver.ConnectionClosed(connectionAuthorizedAndObserved);
                }
            }
        }

        async Task SafelyCloseStreamAsync(Stream stream, string clientName)
        {
            try
            {
                await stream.DisposeAsync();
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to close stream for {0}. This may result in a memory leak. {1}", clientName, ex.Message);
            }
        }

        void SafelyRemoveClientFromTcpClientManager(TcpClient client, string clientName)
        {
            try
            {
                tcpClientManager?.RemoveClient(client);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to release TcpClient from TcpClientManager for {0}. This may result in a memory leak. {1}", clientName, ex.Message);
            }
        }

        async Task SendFriendlyHtmlPage(Stream stream)
        {
            var message = getFriendlyHtmlPageContent();
            var headers = getFriendlyHtmlPageHeaders();

            await stream.WriteLineAsync("HTTP/1.0 200 OK", cancellationToken);
            await stream.WriteLineAsync("Content-Type: text/html; charset=utf-8", cancellationToken);
            await stream.WriteLineAsync("Content-Length: " + message.Length, cancellationToken);

            foreach (var header in headers)
            {
                await stream.WriteLineAsync($"{header.Key}: {header.Value}", cancellationToken);
            }

            await stream.WriteLineAsync(string.Empty, cancellationToken);
            await stream.WriteLineAsync(message, cancellationToken);
            await stream.WriteLineAsync(string.Empty, cancellationToken);
            await stream.FlushAsync();
        }

        static string? GetThumbprint(SslStream stream)
        {
            if (stream.RemoteCertificate == null)
            {
                return null;
            }

            var thumbprint = new X509Certificate2(stream.RemoteCertificate.Export(X509ContentType.Cert), (string)null!).Thumbprint;
            return thumbprint;
        }

        bool Authorize(string thumbprint, string clientName)
        {
            log.Write(EventType.Diagnostic, "Begin authorization");

            var isAuthorized = verifyClientThumbprint(thumbprint);

            if (!isAuthorized)
            {
                log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but it presented a client certificate with the thumbprint '{1}' which is not in the list of thumbprints that we trust", clientName, thumbprint);
                var response = unauthorizedClientConnect(clientName, thumbprint);
                if (response == UnauthorizedClientConnectResponse.BlockConnection)
                    return false;
            }

            log.Write(EventType.Security, "Client at {0} authenticated as {1}", clientName, thumbprint);
            return true;
        }

        Task ExchangeMessages(SslStream stream)
        {
            log.Write(EventType.Diagnostic, "Begin message exchange");

            return exchangeAction(exchangeProtocolBuilder(stream, log), cancellationToken);
        }

        static bool AcceptAnySslCertificate(object sender, X509Certificate? clientCertificate, X509Chain? chain, SslPolicyErrors sslpolicyerrors)
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

        async Task<string> ReadInitialRequest(Stream stream)
        {
            var builder = new StringBuilder();
            var lastWasNewline = false;
            while (builder.Length < 20000)
            {
                var b = await stream.ReadByteAsync(cancellationToken);

                if (b == -1) return builder.ToString();

                var c = (char)b;
                if (c == '\r')
                {
                    // ignore
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

        public async ValueTask DisposeAsync()
        {
            cts.Cancel();
            backgroundThread?.Join();
            if (backgroundTask != null) await backgroundTask;
            listener?.Stop();
            tcpClientManager.Dispose();
            cts.Dispose();
            log?.Write(EventType.ListenerStopped, "Listener stopped");
        }
    }
}