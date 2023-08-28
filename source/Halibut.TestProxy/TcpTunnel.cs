using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Halibut.TestProxy
{
    internal class TcpTunnel : IDisposable
    {
        readonly TcpClient fromClient;
        readonly TcpClient toClient;
        readonly ILogger _logger;
        bool disposedValue;

        public TcpTunnel(TcpClient fromClient, TcpClient toClient, ILogger logger)
        {
            this.fromClient = fromClient;
            this.toClient = toClient;
            _logger = logger;
        }

        public async Task Tunnel(CancellationToken cancellationToken)
        {
#if !NETFRAMEWORK
            await
#endif
            using var fromStream = fromClient.GetStream();
#if !NETFRAMEWORK
            await
#endif
            using var toStream = toClient.GetStream();

            var fromWriter = PipeWriter.Create(fromStream, new StreamPipeWriterOptions(leaveOpen: true));
            var toWriter = PipeWriter.Create(toStream, new StreamPipeWriterOptions(leaveOpen: true));

            try
            {
                var fromStreamTask = fromStream.CopyToAsync(toWriter, cancellationToken);
                var toStreamTask = toStream.CopyToAsync(fromWriter, cancellationToken);

                await Task.WhenAny(fromStreamTask, toStreamTask, cancellationToken.AsTask());
            }
            catch (Exception)
            {
            }
            finally
            {
                await Task.WhenAll(
                    fromWriter.CompleteAsync().AsTask(),
                    toWriter.CompleteAsync().AsTask());

                Try.CatchingError(() => fromStream.Close(), e => { _logger.LogWarning("Error closing fromStream"); });
                Try.CatchingError(() => toStream.Close(), e => { _logger.LogWarning("Error closing toStream"); });
                fromClient.CloseImmediately(_ => { _logger.LogWarning("Error closing fromClient"); });
                toClient.CloseImmediately(_ => { _logger.LogWarning("Error closing toClient"); });
            }
        }

        public override string ToString()
        {
            return $"{fromClient.Client.RemoteEndPoint} <-> {fromClient.Client.LocalEndPoint} <-> {toClient.Client.RemoteEndPoint}";
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    fromClient.Dispose();
                    toClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
