using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Halibut.TestUtils.PortForwarder.TcpUtils;
using Serilog;

namespace Halibut.Tests.Util.TcpUtils
{
    
    /// <summary>
    /// A TCP listener which when a client connects (and so a new TCP connection is created) this
    /// immediately kills that new connection.
    /// </summary>
    public class TCPListenerWhichKillsNewConnections : IDisposable
    {
        readonly Socket listeningSocket;
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly ILogger logger = Log.ForContext<PortForwarder>();
        
        public int Port { get; private set; }
        
        public TCPListenerWhichKillsNewConnections()
        {
            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listeningSocket.Listen(0);
            Port = ((IPEndPoint)listeningSocket.LocalEndPoint).Port;
            Task.Factory.StartNew(() => WorkerTask(cancellationTokenSource.Token).ConfigureAwait(false), TaskCreationOptions.LongRunning);
        }
        
        async Task WorkerTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();

                try
                {
                    var clientSocket = await listeningSocket.AcceptAsync();
                    clientSocket.Close();
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
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            listeningSocket?.Dispose();
        }
    }
}