using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Halibut.Tests.Util
{
    public class TcpPump : IDisposable
    {
        readonly Socket clientSocket;
        readonly EndPoint clientEndPoint;
        readonly Socket originSocket;
        readonly EndPoint originEndPoint;
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly ILogger logger = Log.ForContext<TcpPump>();
        readonly TimeSpan sendDelay;
        bool isDisposing;
        bool isDisposed;
        public bool IsPaused { get; set; }
        

        public TcpPump(Socket clientSocket, Socket originSocket, EndPoint originEndPoint, TimeSpan sendDelay)
        {
            this.clientSocket = clientSocket ?? throw new ArgumentNullException(nameof(clientSocket));
            this.originSocket = originSocket ?? throw new ArgumentNullException(nameof(originSocket));
            this.originEndPoint = originEndPoint ?? throw new ArgumentNullException(nameof(originEndPoint));
            this.sendDelay = sendDelay;
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
                        await originSocket.ConnectAsync(originEndPoint).ConfigureAwait(false);

                        // If the connection was ok, then set-up a pump both ways
                        var pump1 = Task.Run(async () => await PumpBytes(clientSocket, originSocket, new SocketPump(() => this.IsPaused, sendDelay), cancellationToken).ConfigureAwait(false), cancellationToken);
                        var pump2 = Task.Run(async () => await PumpBytes(originSocket, clientSocket, new SocketPump(() => this.IsPaused, sendDelay), cancellationToken).ConfigureAwait(false), cancellationToken);

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
                    clientSocket.Close();
                    originSocket.Close();

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

                    var socketStatus = await socketPump.PumpBytes(readFrom, writeTo, cancellationToken);
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
            }
        }


        public void Dispose()
        {
            if (isDisposing || isDisposed) return;
            isDisposing = true;

            logger.Verbose("Port forwarder disposed.");
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            isDisposed = true;
        }

        public void Pause()
        {
            IsPaused = true;
        }
    }
}