using System;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class ReceiveMessageWithStatistics<T>
    {
        public T receivedMessage { get; }
        public TransferStatistics transferStatistics { get; }
        public TimeSpan timeWaitingForFirstByteOfResponse { get; }

        public ReceiveMessageWithStatistics(T receivedMessage, TransferStatistics transferStatistics, TimeSpan timeWaitingForFirstByteOfResponse)
        {
            this.receivedMessage = receivedMessage;
            this.transferStatistics = transferStatistics;
            this.timeWaitingForFirstByteOfResponse = timeWaitingForFirstByteOfResponse;
        }
    }

    public class ResponseMessageWithTransferStatistics
    {
        public ResponseMessage receivedMessage { get; }
        public TransferStatistics sendStatistics{ get; }
        public TransferStatistics receiveStatistics{ get; }
        public TimeSpan? timeWaitingForFirstByteOfResponse{ get; }

        public ResponseMessageWithTransferStatistics(ResponseMessage receivedMessage, TimeSpan? timeWaitingForFirstByteOfResponse, TransferStatistics sendStatistics, TransferStatistics receiveStatistics)
        {
            this.timeWaitingForFirstByteOfResponse = timeWaitingForFirstByteOfResponse;
            this.sendStatistics = sendStatistics;
            this.receivedMessage = receivedMessage;
            this.receiveStatistics = receiveStatistics;
        }

        public static ResponseMessageWithTransferStatistics WithoutAnyStats(ResponseMessage responseMessage)
        {
            return new ResponseMessageWithTransferStatistics(responseMessage, null, null, null);
        }
    }
}