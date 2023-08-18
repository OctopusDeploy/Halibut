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

    public class SecureListener : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
#pragma warning disable PC003 // Native API not available in UWP
        static extern bool SetHandleInformation(IntPtr hObject, HANDLE_FLAGS dwMask, HANDLE_FLAGS dwFlags);
#pragma warning restore PC003 // Native API not available in UWP

        readonly IPEndPoint endPoint;
        readonly X509Certificate2 serverCertificate;
        readonly ExchangeProtocolBuilder exchangeProtocolBuilder;
        readonly Predicate<string> verifyClientThumbprint;
        readonly Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect;
        readonly ILogFactory logFactory;
        readonly Func<string> getFriendlyHtmlPageContent;
        readonly Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders;
        readonly CancellationTokenSource cts = new();
        readonly TcpClientManager tcpClientManager = new();
        readonly ExchangeActionAsync exchangeAction;
        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        ILog log;
        TcpListener listener;
        Thread backgroundThread;

        public SecureListener(
            IPEndPoint endPoint, 
            X509Certificate2 serverCertificate, 
            ExchangeProtocolBuilder exchangeProtocolBuilder, 
            ExchangeActionAsync exchangeAction, 
            Predicate<string> verifyClientThumbprint, 
            ILogFactory logFactory, 
            Func<string> getFriendlyHtmlPageContent, 
            Func<Dictionary<string, string>> getFriendlyHtmlPageHeaders,
            Func<string, string, UnauthorizedClientConnectResponse> unauthorizedClientConnect, 
            AsyncHalibutFeature asyncHalibutFeature,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
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
            this.asyncHalibutFeature = asyncHalibutFeature;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
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

            backgroundThread = new Thread(Accept)
            {
                Name = "Accept connections on " + listener.LocalEndpoint
            };
            backgroundThread.IsBackground = true;
            backgroundThread.Start();

            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public void Disconnect(string thumbprint)
        {
            tcpClientManager.Disconnect(thumbprint);
        }

        void Accept()
        {
            // See: https://github.com/OctopusDeploy/Issues/issues/6035
            // See: https://github.com/dotnet/corefx/issues/26034

            void WaitForPendingConnectionOrCancellation()
            {
                SpinWait.SpinUntil(() => cts.IsCancellationRequested || listener.Pending());
            }

            const int errorThreshold = 3;

            using (IsWindows() ? cts.Token.Register(listener.Stop) : (IDisposable) null)
            {
                var numberOfFailedAttemptsInRow = 0;
                while (!cts.IsCancellationRequested)
                {
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

                        var client = listener.AcceptTcpClient();
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
                        log.WriteException(EventType.Error, "Error accepting TCP client", ex);
                        // Slow down the logs in case an exception is immediately encountered after X failed AcceptTcpClient calls
                        if (numberOfFailedAttemptsInRow >= errorThreshold)
                        {
                            var millisecondsTimeout = Math.Max(0, Math.Min(numberOfFailedAttemptsInRow - errorThreshold, 100)) * 10;
                            log.Write(
                                EventType.Error,
                                $"Accepting a connection has failed {numberOfFailedAttemptsInRow} times in a row. Waiting {millisecondsTimeout}ms before attempting to accept another connection. For a detailed troubleshooting guide go to https://g.octopushq.com/TentacleTroubleshooting"
                            );
                            Thread.Sleep(millisecondsTimeout);
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
                if (asyncHalibutFeature.IsEnabled())
                {
                    client.SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientSendTimeout.TotalMilliseconds;
                    client.ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientReceiveTimeout.TotalMilliseconds;
                }
                else
                {
#pragma warning disable CS0612
                    client.SendTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalMilliseconds;
                    client.ReceiveTimeout = (int)HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds;
#pragma warning restore CS0612
                }
                

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
            var clientName = client.Client.RemoteEndPoint;
            
            Stream stream = client.GetStream();
            if (asyncHalibutFeature.IsEnabled())
            {
                stream = stream.AsNetworkTimeoutStream();
            }
            
            using (var ssl = new SslStream(stream, true, AcceptAnySslCertificate))
            {
                try
                {
                    log.Write(EventType.SecurityNegotiation, "Performing TLS server handshake");
                    await ssl.AuthenticateAsServerAsync(serverCertificate, true, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false).ConfigureAwait(false);

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
                        // The ExchangeMessage call can hang on reading the stream which keeps a thread alive,
                        // so we dispose the stream which will cause the thread to abort with an exceptions.
                        var weakSSL = new WeakReference(ssl);
                        using (var registration = cts.Token.Register(() =>
                        {
                            if (weakSSL.IsAlive)
                                ((IDisposable)weakSSL.Target).Dispose();
                        }))
                        {
                            tcpClientManager.AddActiveClient(thumbprint, client);
                            await ExchangeMessages(ssl).ConfigureAwait(false);
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
                    SafelyRemoveClientFromTcpClientManager(client, clientName);
                    SafelyCloseStream(stream, clientName);
                    SafelyCloseClient(client, clientName);
                }
            }
        }

        void SafelyCloseClient(TcpClient client, EndPoint clientName)
        {
            try
            {
                client.Close();
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to close TcpClient for {0}. This may result in a memory leak. {1}", clientName, ex.Message);
            }
        }

        void SafelyCloseStream(Stream stream, EndPoint clientName)
        {
            try
            {
                stream.Close();
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to close stream for {0}. This may result in a memory leak. {1}", clientName, ex.Message);
            }
        }

        void SafelyRemoveClientFromTcpClientManager(TcpClient client, EndPoint clientName)
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

            if (asyncHalibutFeature.IsEnabled())
            {
                await stream.WriteLineAsync("HTTP/1.0 200 OK", cts.Token);
                await stream.WriteLineAsync("Content-Type: text/html; charset=utf-8", cts.Token);
                await stream.WriteLineAsync("Content-Length: " + message.Length, cts.Token);
                foreach (var header in headers)
                    await stream.WriteLineAsync($"{header.Key}: {header.Value}", cts.Token);
                await stream.WriteLineAsync(string.Empty, cts.Token);
                await stream.WriteLineAsync(message, cts.Token);
                await stream.WriteLineAsync(string.Empty, cts.Token);
                await stream.FlushAsync();
            }
            else
            {
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
        }

        static string GetThumbprint(SslStream stream)
        {
            if (stream.RemoteCertificate == null)
            {
                return null;
            }

            var thumbprint = new X509Certificate2(stream.RemoteCertificate.Export(X509ContentType.Cert), (string)null!).Thumbprint;
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

            return exchangeAction(exchangeProtocolBuilder(stream, log), cts.Token);
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

        async Task<string> ReadInitialRequest(Stream stream)
        {
            var builder = new StringBuilder();
            var lastWasNewline = false;
            while (builder.Length < 20000)
            {
                var b = asyncHalibutFeature.IsEnabled()
                    ? await stream.ReadByteAsync(cts.Token)
                    : stream.ReadByte();

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
