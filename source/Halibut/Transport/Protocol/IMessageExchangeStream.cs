using System;

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
        void IdentifyAsServer();
        RemoteIdentity ReadRemoteIdentity();
        void Send<T>(T message);
        T Receive<T>();
    }
}