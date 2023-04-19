using System;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public interface IMessageExchangeStream
    {
        void IdentifyAsClient();
        void SendNext();
        void SendProceed();
        Task SendProceedAsync();
        void SendEnd();
        bool ExpectNextOrEnd();
        Task<bool> ExpectNextOrEndAsync();
        void ExpectProceeed();
        void IdentifyAsSubscriber(string subscriptionId);
        void IdentifyAsServer();
        RemoteIdentity ReadRemoteIdentity();
        TransferStatistics Send<T>(T message);
        ReceiveMessageWithStatistics<T> Receive<T>();
    }
}