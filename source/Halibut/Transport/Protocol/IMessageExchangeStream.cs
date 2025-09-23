using System;
using System.Collections.Generic;
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
        
        /// <summary>
        /// When the service is listening, the listening service will block on this call waiting for
        /// the client to send the control message "NEXT". For listening this is the point in the protocol
        /// at which pooled connections will wait at.
        /// 
        /// Note for a polling service, it is the client which waits for the "NEXT" control message
        /// which is sent by the service immediately after sending the response.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> ExpectNextOrEndAsync(TimeSpan readTimeout, CancellationToken cancellationToken);

        Task ExpectProceedAsync(CancellationToken cancellationToken);

        Task IdentifyAsSubscriberAsync(string subscriptionId, CancellationToken cancellationToken);

        Task IdentifyAsServerAsync(CancellationToken cancellationToken);

        Task<RemoteIdentity> ReadRemoteIdentityAsync(CancellationToken cancellationToken);

        Task SendAsync<T>(T message, CancellationToken cancellationToken);
        
        Task SendPrePreparedRequestAsync(PreparedRequestMessage preparedRequestMessage, CancellationToken cancellationToken);

        Task<RequestMessage?> ReceiveRequestAsync(TimeSpan timeoutForReceivingTheFirstByte, CancellationToken cancellationToken);
        Task<ResponseMessage?> ReceiveResponseAsync(CancellationToken cancellationToken);
        
        Task<ResponseBytesAndDataStreams?> ReceiveResponseBytesAsync(CancellationToken cancellationToken);
    }

    public class ResponseBytesAndDataStreams
    {
        public ResponseBytesAndDataStreams(byte[] responseBytes, List<DataStream> dataStreams)
        {
            ResponseBytes = responseBytes;
            DataStreams = dataStreams;
        }

        public byte[] ResponseBytes { get; }
        public List<DataStream> DataStreams { get; }
    }
}