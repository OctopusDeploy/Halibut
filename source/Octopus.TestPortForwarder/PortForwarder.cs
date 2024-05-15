using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using Serilog.Core;

namespace Octopus.TestPortForwarder
{
    public class PortForwarder : IDisposable
    {

        public static bool NoDelay = true;
        readonly Uri originServer;
        Socket? listeningSocket;
        readonly CancellationTokenSource cancellationTokenSource = new();
        readonly List<TcpPump> pumps = new();
        readonly ILogger logger;
        readonly TimeSpan sendDelay;
        readonly int numberOfBytesToDelaySending;
        Func<BiDirectionalDataTransferObserver> biDirectionalDataTransferObserverFactory;
        bool active;

        public int ListeningPort { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="originServer"></param>
        /// <param name="sendDelay"></param>
        /// <param name="biDirectionalDataTransferObserverFactory">Will be created for each new accepted TCP connection by the proxy.</param>
        /// <param name="numberOfBytesToDelaySending"></param>
        /// <param name="logger"></param>
        /// <param name="listeningPort"></param>
        public PortForwarder(Uri originServer,
            TimeSpan sendDelay,
            Func<BiDirectionalDataTransferObserver> biDirectionalDataTransferObserverFactory,
            int numberOfBytesToDelaySending,
            ILogger logger,
            int? listeningPort = null)
        {
            logger = logger.ForContext<PortForwarder>();
            this.originServer = originServer;
            this.sendDelay = sendDelay;
            this.biDirectionalDataTransferObserverFactory = biDirectionalDataTransferObserverFactory;
            this.logger = logger;
            this.numberOfBytesToDelaySending = numberOfBytesToDelaySending;
            var scheme = originServer.Scheme;

            Start();
            var ipEndPoint = listeningSocket.LocalEndPoint as IPEndPoint ?? throw new InvalidOperationException("listeningSocket.LocalEndPoint was not an IPEndPoint");
            
            ListeningPort = ipEndPoint.Port;
            PublicEndpoint = new UriBuilder(scheme, "localhost", ListeningPort).Uri;

            Task.Factory.StartNew(() => WorkerTask(cancellationTokenSource.Token).ConfigureAwait(false), TaskCreationOptions.LongRunning);
        }

        [MemberNotNull(nameof(listeningSocket))]
        private void Start()
        {
            if (active)
            {
                throw new InvalidOperationException("PortForwarder is already started");
            }

            listeningSocket ??= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, NoDelay);

            listeningSocket!.Bind(new IPEndPoint(IPAddress.Loopback, ListeningPort));

            try
            {
                listeningSocket.Listen(int.MaxValue);
            }
            catch (SocketException)
            {
                Stop();
                throw;
            }
            logger.Information("Listening on {LoadBalancerEndpoint}", listeningSocket.LocalEndPoint?.ToString());
            logger.Information("Forwarding to {OriginServer}", originServer);
            active = true;
        }

        private void Stop()
        {
            active = false;
            listeningSocket?.Dispose();
            listeningSocket = null;
            logger.Information("Stopped listening");
            CloseExistingConnections();
        }

        public Uri PublicEndpoint { get; set; }

        public bool KillNewConnectionsImmediatlyMode { get; set; }
        public bool PauseNewConnectionsImmediatelyMode { get; private set; }

        public void EnterKillNewAndExistingConnectionsMode()
        {
            KillNewConnectionsImmediatlyMode = true;
            this.CloseExistingConnections();
        }

        public void EnterPauseNewAndExistingConnectionsMode()
        {
            PauseNewConnectionsImmediatelyMode = true;
            this.PauseExistingConnections();
        }

        public void ReturnToNormalMode()
        {
            KillNewConnectionsImmediatlyMode = false;
            PauseNewConnectionsImmediatelyMode = false;
        }

        async Task WorkerTask(CancellationToken cancellationToken)
        {
            var socket = this.listeningSocket ?? throw new InvalidOperationException("Cannot start WorkerTask with an uninitialized listeningSocket");
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
                if (active)
                {
                    try
                    {
                        var clientSocket = await socket.AcceptAsync();

                        try
                        {
                            // Must be created as soon as the TCP connection is accepted.
                            var biDirectionalDataTransferObserver = biDirectionalDataTransferObserverFactory();

                            if (!active || KillNewConnectionsImmediatlyMode || cancellationToken.IsCancellationRequested)
                            {
                                CloseSocketIgnoringErrors(clientSocket);

                                if (!active) throw new OperationCanceledException("Port forwarder is not active");
                                continue;
                            }

                            var originEndPoint = new DnsEndPoint(originServer.Host, originServer.Port);
                            var originSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                            originSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, NoDelay);

                            var pump = new TcpPump(clientSocket, originSocket, originEndPoint, sendDelay, biDirectionalDataTransferObserver, numberOfBytesToDelaySending, logger);

                            if (PauseNewConnectionsImmediatelyMode)
                            {
                                logger.Information("Pausing the Pump or the new conenction");
                                pump.Pause();
                            }

                            AddNewPump(pump, cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            logger.Verbose(exception, "Error after accepting connection, closing it immediately");
                            CloseSocketIgnoringErrors(clientSocket);
                        }
                    }
                    catch (SocketException ex)
                    {
                        // This will occur normally on teardown.
                        logger.Verbose(ex, "Socket Error accepting new connection {Message}", ex.Message);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error accepting new connection {Message}", ex.Message);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }
        }

        private static void CloseSocketIgnoringErrors(Socket clientSocket)
        {
            DoIgnoringException(() => clientSocket.Shutdown(SocketShutdown.Both));
            DoIgnoringException(() => clientSocket.Close(0));
            DoIgnoringException(() => clientSocket.Dispose());
        }

        private void AddNewPump(TcpPump pump, CancellationToken cancellationToken)
        {

            lock (pumps)
            {
                if (cancellationToken.IsCancellationRequested || !active || KillNewConnectionsImmediatlyMode)
                {
                    try
                    {
                        pump.Dispose();
                    }
                    catch (Exception)
                    {
                        // if we encounter an error during cancellation there's not a lot that we can do about that
                    }
                }
                else
                {
                    pump.Stopped += OnPortForwarderStopped;
                    pumps.Add(pump);
                }
            }

            pump.Start();
        }

        void OnPortForwarderStopped(object? sender, EventArgs e)
        {
            if (sender is TcpPump portForwarder)
            {
                portForwarder.Stopped -= OnPortForwarderStopped;
                lock (pumps)
                {
                    pumps.Remove(portForwarder);
                }

                portForwarder.Dispose();
            }
        }

        public void UnPauseExistingConnections()
        {
            lock (pumps)
            {
                foreach (var pump in pumps)
                {
                    pump.UnPause();
                }
            }
        }

        public void PauseExistingConnections()
        {
            logger.Information("Pausing existing connections");
            lock (pumps)
            {
                foreach (var pump in pumps)
                {
                    pump.Pause();
                }
            }
        }

        public void CloseExistingConnections()
        {
            logger.Information("Closing existing connections");
            DisposePumps();
        }

        List<Exception> DisposePumps()
        {
            logger.Information("Start Dispose Pumps");
            var exceptions = new List<Exception>();

            lock (pumps)
            {
                var clone = pumps.ToArray();
                pumps.Clear();
                foreach (var pump in clone)
                {
                    try
                    {
                        pump.Dispose();
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }
            }

            logger.Information("Fisinshed Dispose Pumps");

            return exceptions;
        }

        public void Dispose()
        {
            if(!cancellationTokenSource.IsCancellationRequested) cancellationTokenSource.Cancel();

            var exceptions = DisposePumps();

            try
            {
                listeningSocket?.Shutdown(SocketShutdown.Both);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            try
            {
                listeningSocket?.Close(0);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            try
            {
                listeningSocket?.Dispose();
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            try
            {
                cancellationTokenSource.Dispose();
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }

            if (exceptions.Count(x => x is not ObjectDisposedException &&
                    !(x is SocketException && x.Message.Contains("A request to send or receive data was disallowed because the socket is not connected"))) > 0)
            {
                logger.Warning(new AggregateException(exceptions), "Exceptions where thrown when Disposing of the PortForwarder");
            }
        }

        private static void DoIgnoringException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                // if we encounter an error during shutdown there's not a lot that we can do about that
            }
        }
    }
}
