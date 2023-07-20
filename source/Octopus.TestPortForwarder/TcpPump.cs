using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.TestPortForwarder
{
    public class TcpPump : IDisposable
    {
        static long numberOfTcpPumpsCreated = 0;

        static long NextTcpPumpNumber()
        {
            return Interlocked.Increment(ref numberOfTcpPumpsCreated);
        }
        readonly Socket clientSocket;
        readonly EndPoint clientEndPoint;
        readonly Socket originSocket;
        readonly EndPoint originEndPoint;
        readonly CancellationTokenSource cancellationTokenSource = new();
        readonly ILogger logger;
        readonly TimeSpan sendDelay;
        readonly int numberOfBytesToDelaySending;
        bool isDisposing;
        bool isDisposed;
        public bool IsPaused { get; set; }
        private BiDirectionalDataTransferObserver biDirectionalDataTransferObserver;
        public long PumpNumber { get; } = NextTcpPumpNumber();

        public TcpPump(Socket clientSocket, Socket originSocket, EndPoint originEndPoint, TimeSpan sendDelay, BiDirectionalDataTransferObserver biDirectionalDataTransferObserver, int numberOfBytesToDelaySending, ILogger logger)
        {
            this.logger = logger.ForContext<TcpPump>();
            this.clientSocket = clientSocket ?? throw new ArgumentNullException(nameof(clientSocket));
            this.originSocket = originSocket ?? throw new ArgumentNullException(nameof(originSocket));
            this.originEndPoint = originEndPoint ?? throw new ArgumentNullException(nameof(originEndPoint));
            this.sendDelay = sendDelay;
            this.biDirectionalDataTransferObserver = biDirectionalDataTransferObserver;
            this.numberOfBytesToDelaySending = numberOfBytesToDelaySending;
            clientEndPoint = clientSocket.RemoteEndPoint ?? throw new ArgumentException("Remote endpoint is null", nameof(clientSocket));
        }
        
        public event EventHandler<EventArgs> Stopped;

        public void Start()
        {
            var cancellationToken = cancellationTokenSource.Token;

            Task.Run(
                async () =>
                {
                    logger.Verbose("Forwarding connection from {ClientEndPoint} to {OriginEndPoint}.", clientEndPoint.ToString(), originEndPoint.ToString());

                    try
                    {
#if DOES_NOT_SUPPORT_CANCELLATION_ON_SOCKETS
                        var connectAsyncCancellationTokenSource = new CancellationTokenSource();
                        using (cancellationToken.Register(() => connectAsyncCancellationTokenSource.Cancel()))
                        {
                            var cancelTask = connectAsyncCancellationTokenSource.Token.AsTask();
                            var actionTask = originSocket.ConnectAsync(originEndPoint);

                            await (await Task.WhenAny(actionTask, cancelTask).ConfigureAwait(false)).ConfigureAwait(false);
                        }
#else
                        await originSocket.ConnectAsync(originEndPoint, cancellationToken).ConfigureAwait(false);
#endif

                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // If the connection was ok, then set-up a pump both ways
                        var pump1 = Task.Run(async () => await PumpBytes(clientSocket, originSocket, new SocketPump(this, () => this.IsPaused, sendDelay, biDirectionalDataTransferObserver.DataTransferObserverClientToOrigin, numberOfBytesToDelaySending, logger), cancellationToken).ConfigureAwait(false), cancellationToken);
                        var pump2 = Task.Run(async () => await PumpBytes(originSocket, clientSocket, new SocketPump(this, () => this.IsPaused, sendDelay, biDirectionalDataTransferObserver.DataTransferObserverOriginToClient, numberOfBytesToDelaySending, logger), cancellationToken).ConfigureAwait(false), cancellationToken);

                        // When one is finished, they are both "done" so stop them
                        await Task.WhenAny(pump1, pump2).ConfigureAwait(false);
                        logger.Verbose("Stopping connection forwarding from {ClientEndPoint} to {OriginEndPoint}.", clientEndPoint.ToString(), originEndPoint.ToString());

                        if (!cancellationTokenSource.IsCancellationRequested)
                        {
                            cancellationTokenSource.Cancel();
                        }

                        // Wait for both pumps to be complete
                        await Task.WhenAll(pump1, pump2).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.Verbose(ex, "Forwarding between {ClientEndPoint} and {OriginEndPoint} failed.", clientEndPoint.ToString(), originEndPoint.ToString());
                    }

                    // We are done. Close everything.
                    logger.Verbose("Stopped connection forwarding from {ClientEndPoint} to {OriginEndPoint}.", clientEndPoint.ToString(), originEndPoint.ToString());
                    clientSocket.Close(0);
                    originSocket.Close(0);

                    // Let the users know we are done
                    Stopped?.Invoke(this, EventArgs.Empty);
                },
                cancellationToken);
        }

        async Task PumpBytes(Socket readFrom, Socket writeTo, SocketPump socketPump, CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Yield();

                if (cancellationToken.IsCancellationRequested) break;
                if (!readFrom.Connected) break;
                if (!writeTo.Connected) break;

                try
                {

                    var socketStatus = await socketPump.PumpBytes(readFrom, writeTo, cancellationToken).ConfigureAwait(false);
                    await socketPump.PausePump(cancellationToken);
                    if(socketStatus == SocketPump.SocketStatus.SOCKET_CLOSED) break;
                }
                catch (SocketException socketException)
                {
                    logger.Verbose(socketException, "Received socket error {SocketErrorCode} {ReadEndPoint}.", socketException.SocketErrorCode, readFrom.ToString());
                }
                catch (OperationCanceledException canceledException)
                {
                    logger.Verbose(canceledException, "Received pump cancellation {ReadEndPoint}.", readFrom.ToString());
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Received pump exception: {Message}.", ex.Message);
                }
                await socketPump.PausePump(cancellationToken);
            }
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void UnPause()
        {
            IsPaused = false;
        }

        public void Dispose()
        {
            if (isDisposing || isDisposed) return;
            isDisposing = true;

            logger.Verbose("Port forwarder disposed.");
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            isDisposed = true;

            var exceptions = new List<Exception>();

            try
            {
                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            try
            {
                originSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            try
            {
                clientSocket.Close(0);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            try
            {
                originSocket.Close(0);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            try
            {
                clientSocket.Dispose();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            try
            {
                originSocket.Dispose();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            if (exceptions.Count(x => x is not ObjectDisposedException) > 0)
            {
                logger.Warning(new AggregateException(exceptions), "Errors occurred Disposing of the TcpPump");
            }
        }
    }
}