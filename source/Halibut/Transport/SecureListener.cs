using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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
using Halibut.Transport.Protocol;
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

    public class SecureListener : IDisposable, Stoppable
    {
#if CAN_GET_SOCKET_HANDLE
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetHandleInformation(IntPtr hObject, HANDLE_FLAGS dwMask, HANDLE_FLAGS dwFlags);
#endif
        readonly IPEndPoint endPoint;
        X509Certificate2 serverCertificate;
        readonly Func<MessageExchangeProtocol, Task> protocolHandler;
        readonly Predicate<string> verifyClientThumbprint;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        CancellationTokenSource cts = new CancellationTokenSource();
        ILog log;
        TcpListener listener;
        readonly ConcurrentDictionary<Task, Task> runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
        Task acceptPumpTask;

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, Action<MessageExchangeProtocol> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
            : this(endPoint, serverCertificate, h => Task.Run(() => protocolHandler(h)), verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent)

        {
        }

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, Func<MessageExchangeProtocol, Task> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
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
            if (endPoint.Address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                listener.Server.DualMode = true;
            }
            listener.Start();

#if CAN_GET_SOCKET_HANDLE
            // set socket handle as not inherited so that when tentacle runs powershell
            // with System.Diagnostics.Process those scripts don't lock the socket
            SetHandleInformation(listener.Server.Handle, HANDLE_FLAGS.INHERIT, HANDLE_FLAGS.None);
#endif

            log = logFactory.ForEndpoint(new Uri("listen://" + listener.LocalEndpoint));
            log.Write(EventType.ListenerStarted, "Listener started");
            acceptPumpTask = Task.Run(Accept, CancellationToken.None);

            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public async Task Stop()
        {
            cts.Cancel();
            
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

            listener.Stop();

            log.Write(EventType.ListenerStopped, "Listener stopped");
        }

        async Task Accept()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    
                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    var receiveTask = HandleClient(client);

                    runningReceiveTasks.TryAdd(receiveTask, receiveTask);
                    log.Write(EventType.Error, "added task");

                    receiveTask.ContinueWith(t =>
                        {
                            Task toBeRemoved;
                            runningReceiveTasks.TryRemove(receiveTask, out toBeRemoved);
                            log.Write(EventType.Error, "removed task");
                        }, TaskContinuationOptions.ExecuteSynchronously)
                        .Ignore();
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Error accepting TCP client", ex);
                    // Slow down the logs in case an exception is immediately encountered each AcceptTcpClientAsync
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }
        }

        async Task HandleClient(TcpClient client)
        {
            try
            {
                client.SendTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalMilliseconds;
                client.ReceiveTimeout = (int)HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds;

                log.Write(EventType.ListenerAcceptedClient, "Accepted TCP client: {0}", client.Client.RemoteEndPoint);
                await ExecuteRequest(client).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Error initializing TCP client", ex);
            }
        }

        async Task ExecuteRequest(TcpClient client)
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
                    await ssl.AuthenticateAsServerAsync(serverCertificate, true, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false).ConfigureAwait(false);

                    log.Write(EventType.Security, "Secure connection established, client is not yet authenticated, client connected with {0}", ssl.SslProtocol.ToString());

                    var req = await ReadInitialRequest(ssl).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(req))
                    {
                        log.Write(EventType.Diagnostic, "Ignoring empty request");
                        return;
                    }

                    if (req.Substring(0, 2) != "MX")
                    {
                        log.Write(EventType.Diagnostic, "Appears to be a web browser, sending friendly HTML response");
                        await SendFriendlyHtmlPage(ssl).ConfigureAwait(false);
                        return;
                    }

                    if (Authorize(ssl, clientName))
                    {
                        // Delegate the open stream to the protocol handler - we no longer own the stream lifetime
                        await ExchangeMessages(ssl).ConfigureAwait(false);

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
#if NET40
                        stream.Close();
                        client.Close();
#else
                        stream.Dispose();
                        client.Dispose();
#endif
                    }
                }
            }
        }

        async Task SendFriendlyHtmlPage(Stream stream)
        {
            var message = getFriendlyHtmlPageContent();

            // This could fail if the client terminates the connection and we attempt to write to it
            // Disposing the StreamWriter will close the stream - it owns the stream
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                await writer.WriteLineAsync("HTTP/1.0 200 OK").ConfigureAwait(false);
                await writer.WriteLineAsync("Content-Type: text/html; charset=utf-8").ConfigureAwait(false);
                await writer.WriteLineAsync("Content-Length: " + message.Length).ConfigureAwait(false);
                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer.WriteLineAsync(message).ConfigureAwait(false);
                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
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

            var thumbprint = new X509Certificate2(stream.RemoteCertificate.Export(X509ContentType.Cert)).Thumbprint;
            var isAuthorized = verifyClientThumbprint(thumbprint);

            if (!isAuthorized)
            {
                log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but it presented a client certificate with the thumbprint '{1}' which is not in the list of thumbprints that we trust", clientName, thumbprint);
                return false;
            }

            log.Write(EventType.Security, "Client at {0} authenticated as {1}", clientName, thumbprint);
            return true;
        }

        Task ExchangeMessages(SslStream stream)
        {
            log.Write(EventType.Diagnostic, "Begin message exchange");

            return protocolHandler(new MessageExchangeProtocol(stream, log));
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

        async Task<int> ReadByteAsync(Stream stream)
        {
            var buffer = new byte[1];
            if ( await stream.ReadAsync(buffer, 0, 1).ConfigureAwait(false) == 0)
                return -1;
            return buffer[0];
        }
        async Task<string> ReadInitialRequest(Stream stream)
        {
            var builder = new StringBuilder();
            var lastWasNewline = false;
            while (builder.Length < 20000)
            {
                var b = await ReadByteAsync(stream).ConfigureAwait(false);
                if (b == -1) return builder.ToString();

                var c = (char)b;
                if (c == '\r')
                {
                    continue;
                }

                if (c == '\n')
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
            // Injected by Fody.Janitor
        }
    }
}