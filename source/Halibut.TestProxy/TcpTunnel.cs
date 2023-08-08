using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestProxy
{
    internal class TcpTunnel : IDisposable
    {
        readonly TcpClient fromClient;
        readonly TcpClient toClient;
        bool disposedValue;

        public TcpTunnel(TcpClient fromClient, TcpClient toClient)
        {
            this.fromClient = fromClient;
            this.toClient = toClient;
        }

        public async Task Tunnel(CancellationToken cancellationToken)
        {
            using var fromStream = fromClient.GetStream();
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
                await fromWriter.CompleteAsync();
                await toWriter.CompleteAsync();
                fromStream.Close();
                toStream.Close();
                fromClient.Close();
                toClient.Close();
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
