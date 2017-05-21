using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public interface IMessageExchangeStream
    {
        Task IdentifyAsClient();
        Task SendNext();
        Task SendProceed();
        Task<bool> ExpectNextOrEnd();
        Task ExpectProceeed();
        Task IdentifyAsSubscriber(string subscriptionId);
        Task IdentifyAsServer();
        Task<RemoteIdentity> ReadRemoteIdentity();
        Task Send<T>(T message);
        Task<T> Receive<T>();
    }
}