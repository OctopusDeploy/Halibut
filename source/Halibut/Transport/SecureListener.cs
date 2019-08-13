using System;
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
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    [Flags]
    enum HANDLE_FLAGS : uint
    {
        None = 0,
        INHERIT = 1,
        PROTECT_FROM_CLOSE = 2
    }

    public class SecureListener : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
#pragma warning disable PC003 // Native API not available in UWP
        static extern bool SetHandleInformation(IntPtr hObject, HANDLE_FLAGS dwMask, HANDLE_FLAGS dwFlags);
#pragma warning restore PC003 // Native API not available in UWP

        readonly IPEndPoint endPoint;
        readonly X509Certificate2 serverCertificate;
        readonly Func<MessageExchangeProtocol, Task> protocolHandler;
        readonly Predicate<string> verifyClientThumbprint;
        readonly Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        readonly Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly TcpClientManager tcpClientManager = new TcpClientManager();
        ILog log;
        TcpListener listener;
        Thread backgroundThread;

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, Action<MessageExchangeProtocol> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
            : this(endPoint, serverCertificate, h => Task.Run(() => protocolHandler(h)), verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, () => new Dictionary<string, string>())

        {
        }

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, Action<MessageExchangeProtocol> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders)
            : this(endPoint, serverCertificate, h => Task.Run(() => protocolHandler(h)), verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, getFriendlyHtmlPageHeaders)

        {
        }

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, Func<MessageExchangeProtocol, Task> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent)
            : this(endPoint, serverCertificate, protocolHandler, verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, () => new Dictionary<string, string>())
        {
        }

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, Func<MessageExchangeProtocol, Task> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders) :
            this(endPoint, serverCertificate, protocolHandler, verifyClientThumbprint, logFactory, getFriendlyHtmlPageContent, getFriendlyHtmlPageHeaders, (clientName, thumbprint) => UnauthorizedClientConnectResponse.BlockConnection)
        {
        }

        public SecureListener(IPEndPoint endPoint, X509Certificate2 serverCertificate, Func<MessageExchangeProtocol, Task> protocolHandler, Predicate<string> verifyClientThumbprint, ILogFactory logFactory, Func<string> getFriendlyHtmlPageContent, Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders, Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect)
        {
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

        public int Start()
        {
            listener = new TcpListener(endPoint);
            if (endPoint.Address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                listener.Server.DualMode = true;
            }
            listener.Start();

#if !NETSTANDARD2_0
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
            {
                // set socket handle as not inherited so that when tentacle runs powershell
                // with System.Diagnostics.Process those scripts don't lock the socket
                SetHandleInformation(listener.Server.Handle, HANDLE_FLAGS.INHERIT, HANDLE_FLAGS.None);
            }

            log = logFactory.ForEndpoint(new Uri("listen://" + listener.LocalEndpoint));
            log.Write(EventType.ListenerStarted, "Listener started");

            backgroundThread = new Thread(Accept)
            {
                Name = "Accept connections on " + listener.LocalEndpoint
            };
            backgroundThread.Start();

            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public void Disconnect(string thumbprint)
        {
            tcpClientManager.Disconnect(thumbprint);
        }

        void Accept()
        {
            var numberOfFailedAttemptsInRow = 0;

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    SpinWait.SpinUntil(() => cts.IsCancellationRequested || listener.Pending());

                    if (cts.IsCancellationRequested)
                    {
                        continue;
                    }

                    var client = listener.AcceptTcpClient();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    HandleClient(client);
#pragma warning restore CS4014
                    numberOfFailedAttemptsInRow = 0;
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    numberOfFailedAttemptsInRow++;
                    log.WriteException(EventType.Error, $"Error accepting TCP client", ex);
                    // Slow down the logs in case an exception is immediately encountered after 3 failed AcceptTcpClient calls
                    if (numberOfFailedAttemptsInRow >= 3)
                    {
                        var millisecondsTimeout = Math.Max(0, Math.Min(numberOfFailedAttemptsInRow - 3, 100)) * 10;
                        log.Write(
                            EventType.Error,
                            $"Accepting a connection has failed {numberOfFailedAttemptsInRow} times in a row. Waiting {millisecondsTimeout}ms before attempting to accept another connection. For a detailed troubleshooting guide go to https://g.octopushq.com/TentacleTroubleshooting"
                        );
                        Thread.Sleep(millisecondsTimeout);
                    }
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
                await ExecuteRequest(client);
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
                    log.Write(EventType.SecurityNegotiation, "Performing TLS server handshake");
                    await ssl.AuthenticateAsServerAsync(serverCertificate, true, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);

                    log.Write(EventType.SecurityNegotiation, "Secure connection established, client is not yet authenticated, client connected with {0}", ssl.SslProtocol.ToString());

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

                    var thumbprint = GetThumbprint(ssl);
                    if (thumbprint == null)
                    {
                        log.Write(EventType.ClientDenied, "A client at {0} connected, and attempted a message exchange, but did not present a client certificate", clientName);
                        return;
                    }

                    if (Authorize(thumbprint, clientName))
                    {
                        // The ExchangeMessage call can hang on reading the stream which keeps a thread alive,
                        // so we dispose the stream which will cause the thread to abort with an exceptions.
                        var weakSSL = new WeakReference(ssl);
                        cts.Token.Register(() =>
                        {
                            if (weakSSL.IsAlive)
                                ((IDisposable)weakSSL.Target).Dispose();
                        });

                        tcpClientManager.AddActiveClient(thumbprint, client);
                        // Delegate the open stream to the protocol handler - we no longer own the stream lifetime
                        await ExchangeMessages(ssl);

                        if (verifyClientThumbprint(thumbprint))	
                        {	
                            // Mark the stream as delegated once everything has succeeded
                            keepStreamOpen = true;
                        }
                    }
                }
                catch (AuthenticationException ex)
                {
                    log.WriteException(EventType.ClientDenied, "Client failed authentication: {0}", ex, clientName);
                }
                catch (IOException ex) when (ex.InnerException is SocketException)
                {
                    log.WriteException(EventType.Error, "Socket IO exception: {0}", ex.InnerException, clientName);
                }
                catch (IOException ex) when (ex.InnerException is ObjectDisposedException)
                {
                    log.WriteException(EventType.ListenerStopped, "Socket IO exception: {0}", ex.InnerException, clientName);
                }
                catch (SocketException ex)
                {
                    log.WriteException(EventType.Error, "Socket exception: {0}", ex, clientName);
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
            var headers = getFriendlyHtmlPageHeaders();

            // This could fail if the client terminates the connection and we attempt to write to it
            // Disposing the StreamWriter will close the stream - it owns the stream
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n" })
            {
                writer.WriteLine("HTTP/1.0 200 OK");
                writer.WriteLine("Content-Type: text/html; charset=utf-8");
                writer.WriteLine("Content-Length: " + message.Length);
                foreach (var header in headers)
                    writer.WriteLine($"{header.Key}: {header.Value}");
                writer.WriteLine();
                writer.WriteLine(message);
                writer.WriteLine();
                writer.Flush();
                stream.Flush();
            }
        }

        static string GetThumbprint(SslStream stream)
        {
            if (stream.RemoteCertificate == null)
            {
                return null;
            }

            var thumbprint = new X509Certificate2(stream.RemoteCertificate.Export(X509ContentType.Cert)).Thumbprint;
            return thumbprint;
        }

        bool Authorize(string thumbprint, EndPoint clientName)
        {
            if (thumbprint == null)
                return false;

            log.Write(EventType.Diagnostic, "Begin authorization");

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

        string ReadInitialRequest(Stream stream)
        {
            var builder = new StringBuilder();
            var lastWasNewline = false;
            while (builder.Length < 20000)
            {
                var b = stream.ReadByte();
                if (b == -1) return builder.ToString();

                var c = (char)b;
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
            cts.Cancel();
            backgroundThread?.Join();
            listener?.Stop();
            cts.Dispose();
            log?.Write(EventType.ListenerStopped, "Listener stopped");
        }
    }
}
