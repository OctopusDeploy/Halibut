using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Halibut.Services
{
    public class SecureConnection : IDisposable
    {
        readonly TcpClient client;
        readonly SslStream stream;
        readonly MessageExchangeProtocol protocol;
        readonly long id;
        static long connectionId;

        public SecureConnection(TcpClient client, SslStream stream, MessageExchangeProtocol protocol)
        {
            this.client = client;
            this.stream = stream;
            this.protocol = protocol;
            id = Interlocked.Increment(ref connectionId);
        }

        public string Id { get { return id.ToString(); } }
        public MessageExchangeProtocol Protocol { get { return protocol; } }
        public string RemoteThumbprint { get { return new X509Certificate2(stream.RemoteCertificate).Thumbprint; } }

        public void Dispose()
        {
            stream.Dispose();
            client.Close();
        }
    }
}