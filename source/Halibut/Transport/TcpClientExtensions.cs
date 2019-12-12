using System;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport
{
    public static class TcpClientExtensions
    {
        [Obsolete]
        public static void ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout)
        {
            ConnectWithTimeout(client, remoteUri.Host, remoteUri.Port, timeout, CancellationToken.None);
        }

        public static void ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout, CancellationToken cancellationToken)
        {
            ConnectWithTimeout(client, remoteUri.Host, remoteUri.Port, timeout, cancellationToken);
        }

        [Obsolete]
        public static void ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout)
        {
            ConnectWithTimeout(client, host, port, timeout, CancellationToken.None);
        }

        public static void ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Connect(client, host, port, timeout, cancellationToken).GetAwaiter().GetResult();
        }
        
        static async Task Connect(TcpClient client, string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                await TimeoutAfter(client.ConnectAsync(host, port), timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                DisposeClient();
                throw new HalibutClientException($"The client was unable to establish the initial connection within {timeout}.");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                DisposeClient();
                //todo make these exceptions have a diff message
                throw new HalibutClientException($"The client was unable to establish the initial connection within {timeout}.");
            }
            catch (Exception ex)
            {
                DisposeClient();
                ExceptionDispatchInfo.Capture(ex).Throw();
            }
            void DisposeClient()
            {
                try
                {
                    ((IDisposable) client).Dispose();
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        static Task AsTask(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();
            cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
            return tcs.Task;
        }
        
        //todo: move to an extension method (TaskExtensions(?))
        //todo: add xmldoc comments
        //todo: unit tests
        static async Task TimeoutAfter(this Task task, TimeSpan timespan, CancellationToken cancellationToken)
        {
            var timeOutTask = Task.Delay(timespan);
            var cancellationTask = cancellationToken.AsTask();
            var timeoutCancellation = new CancellationTokenSource();
            var wrappedTask = AwaitAndSwallowExceptionsWhenCancelled(timeoutCancellation.Token, task);
            var completedTask = await Task.WhenAny(wrappedTask, timeOutTask, cancellationTask);
            if (completedTask == timeOutTask)
            {
                timeoutCancellation.Cancel();
                if (wrappedTask.IsCompleted)
                {
                    await wrappedTask;
                }
                throw new TimeoutException();
            }

            if (completedTask == cancellationTask)
            {
                timeoutCancellation.Cancel();
                throw new OperationCanceledException();
            }
            await wrappedTask;
        }
        
        static async Task AwaitAndSwallowExceptionsWhenCancelled(CancellationToken cancellationToken, Task task)
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            }
        }
    }
}