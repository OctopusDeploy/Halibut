using System;

namespace Halibut.Exceptions
{
    public abstract class RequestCancelledException : OperationCanceledException
    {
        protected RequestCancelledException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class ConnectingRequestCancelledException : RequestCancelledException
    {
        public ConnectingRequestCancelledException(Exception innerException)
            : this("The Request was cancelled while Connecting.", innerException)
        {
        }

        public ConnectingRequestCancelledException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        public ConnectingRequestCancelledException(string message, string innerException)
            : base(message, new Exception(innerException))
        {
        }
    }

    public class TransferringRequestCancelledException : RequestCancelledException
    {
        public TransferringRequestCancelledException(Exception innerException)
            : this("The Request was cancelled while Transferring.", innerException)
        {
        }

        public TransferringRequestCancelledException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public TransferringRequestCancelledException(string message, string innerException)
            : base(message, new Exception(innerException))
        {
        }
    }
}