using System;
using System.Net.Sockets;

namespace Halibut.TestProxy
{
    public static class TcpClientExtensionMethods
    {
        public static void CloseImmediately(this TcpClient client)
        {
            client.CloseImmediately(_ => { });
        }

        public static void CloseImmediately(this TcpClient client, Action<Exception> onError)
        {
            Try.CatchingError(() => client.Client.Close(0), onError);
            Try.CatchingError(client.Close, onError);
        }
    }
}
