using System;
using System.IO;
using System.Net.Sockets;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class TransportTimeoutClassifierFixture
    {
        [Test]
        public void SocketTimeoutWrappedInIOExceptionAndHalibutClientException_IsATransportTimeout()
        {
            var exception = new HalibutClientException("An error occurred when sending a request",
                new IOException("Unable to read data from the transport connection",
                    new SocketException((int)SocketError.TimedOut)));

            TransportTimeoutClassifier.IsTransportTimeout(exception).Should().BeTrue();
        }

        [Test]
        public void Net48TimeoutMessageWithoutInnerException_IsATransportTimeout()
        {
            var exception = new HalibutClientException("Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.");

            TransportTimeoutClassifier.IsTransportTimeout(exception).Should().BeTrue();
        }

        [Test]
        public void PollingRequestNotCollectedInTime_IsATransportTimeout()
        {
            var exception = new HalibutClientException("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (00:01:06), so the request timed out.");

            TransportTimeoutClassifier.IsTransportTimeout(exception).Should().BeTrue();
        }

        [Test]
        public void TimeoutInsideAggregateException_IsATransportTimeout()
        {
            var exception = new AggregateException(new InvalidOperationException("other"), new SocketException((int)SocketError.TimedOut));

            TransportTimeoutClassifier.IsTransportTimeout(exception).Should().BeTrue();
        }

        [Test]
        public void ConnectionRefused_IsNotATransportTimeout()
        {
            var exception = new HalibutClientException("An error occurred when sending a request",
                new SocketException((int)SocketError.ConnectionRefused));

            TransportTimeoutClassifier.IsTransportTimeout(exception).Should().BeFalse();
        }

        [Test]
        public void ProtocolError_IsNotATransportTimeout()
        {
            TransportTimeoutClassifier.IsTransportTimeout(new ProtocolException("Expected PROCEED, got something else")).Should().BeFalse();
        }

        [Test]
        public void AssertionFailure_IsNotATransportTimeout()
        {
            TransportTimeoutClassifier.IsTransportTimeout(new AssertionException("Expected 1 but was 2")).Should().BeFalse();
        }

        [Test]
        public void Null_IsNotATransportTimeout()
        {
            TransportTimeoutClassifier.IsTransportTimeout(null).Should().BeFalse();
        }
    }
}
