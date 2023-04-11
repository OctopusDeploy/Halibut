using System;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public interface IMessageExchangeStream
    {
        RemoteIdentity IdentifyAsClient();
        void SendNext();
        void SendProceed();
        Task SendProceedAsync();
        void SendEnd();
        bool ExpectNextOrEnd();
        Task<bool> ExpectNextOrEndAsync();
        void ExpectProceeed();
        RemoteIdentity IdentifyAsSubscriber(string subscriptionId);
        void IdentifyAsServer();
        RemoteIdentity ReadRemoteIdentity();
        void Send<T>(T message);
        T Receive<T>();
        Version LocalVersion { get; }
    }
}