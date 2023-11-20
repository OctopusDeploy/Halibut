using System;

namespace Halibut.Diagnostics
{
    public class SendReceiveTimeout
    {
        public SendReceiveTimeout(TimeSpan sendTimeout, TimeSpan receiveTimeout)
        {
            SendTimeout = sendTimeout;
            ReceiveTimeout = receiveTimeout;
        }

        public TimeSpan SendTimeout { get; }
        public TimeSpan ReceiveTimeout { get; }
    }
}
