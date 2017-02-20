using System;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public interface IMessageExchangeStream
    {
        void IdentifyAsClient();
        void SendNext();
        void SendProceed();
        bool ExpectNextOrEnd();
        void ExpectProceeed();
        void IdentifyAsSubscriber(string subscriptionId);
        Task IdentifyAsServer();
        RemoteIdentity ReadRemoteIdentity();
        void Send<T>(T message);
        T Receive<T>();
    }
}