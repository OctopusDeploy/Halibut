using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public interface IMessageExchangeStream
    {
        [Obsolete]
        void IdentifyAsClient();
        Task IdentifyAsClientAsync(CancellationToken cancellationToken);

        [Obsolete]
        void SendNext();
        Task SendNextAsync(CancellationToken cancellationToken);

        [Obsolete]
        void SendProceed();
        [Obsolete]
        Task SendProceedAsync();
        Task SendProceedAsync(CancellationToken cancellationToken);

        [Obsolete]
        void SendEnd();
        Task SendEndAsync(CancellationToken cancellationToken);

        [Obsolete]
        bool ExpectNextOrEnd();
        [Obsolete]
        Task<bool> ExpectNextOrEndAsync();
        Task<bool> ExpectNextOrEndAsync(CancellationToken cancellationToken);

        [Obsolete]
        void ExpectProceeed();
        Task ExpectProceedAsync(CancellationToken cancellationToken);

        [Obsolete]
        void IdentifyAsSubscriber(string subscriptionId);
        Task IdentifyAsSubscriberAsync(string subscriptionId, CancellationToken cancellationToken);

        [Obsolete]
        void IdentifyAsServer();
        Task IdentifyAsServerAsync(CancellationToken cancellationToken);

        [Obsolete] 
        RemoteIdentity ReadRemoteIdentity();
        Task<RemoteIdentity> ReadRemoteIdentityAsync(CancellationToken cancellationToken);

        [Obsolete]
        void Send<T>(T message);
        Task SendAsync<T>(T message, CancellationToken cancellationToken);

        [Obsolete]
        T Receive<T>();
        Task<T> ReceiveAsync<T>(CancellationToken cancellationToken);
    }
}