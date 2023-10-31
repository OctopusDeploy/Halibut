using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public interface IMessageExchangeStream
    {
        Task IdentifyAsClientAsync(CancellationToken cancellationToken);
        Task SendNextAsync(CancellationToken cancellationToken);

        Task SendProceedAsync(CancellationToken cancellationToken);

        Task SendEndAsync(CancellationToken cancellationToken);

        Task<bool> ExpectNextOrEndAsync(CancellationToken cancellationToken);

        Task ExpectProceedAsync(CancellationToken cancellationToken);

        Task IdentifyAsSubscriberAsync(string subscriptionId, CancellationToken cancellationToken);

        Task IdentifyAsServerAsync(CancellationToken cancellationToken);

        Task<RemoteIdentity> ReadRemoteIdentityAsync(CancellationToken cancellationToken);

        Task SendAsync<T>(T message, CancellationToken cancellationToken);

        Task<T> ReceiveAsync<T>(CancellationToken cancellationToken);
    }
}