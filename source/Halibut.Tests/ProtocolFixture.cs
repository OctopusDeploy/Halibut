using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ProtocolFixture
    {
        MessageExchangeProtocol protocol;
        DumpStream stream;

        [SetUp]
        public void SetUp()
        {
            stream = new DumpStream();
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Server));
            protocol = new MessageExchangeProtocol(stream);
        }

        [Test]
        public void ShouldExchangeAsClient()
        {
            protocol.ExchangeAsClient(GetOutgoingRequestMessage());

            AssertOutput(@"
--> MX-CLIENT
<-- MX-SERVER
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope");
        }

        [Test]
        public void ShouldExchangeAsServerOfClient()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(GetIncomingRequestMessage());
            stream.SetNumberOfReads(1);

            protocol.ExchangeAsServer(req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), ri => new PendingRequestQueue(new InMemoryConnectionLog("x")));

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
<-- END");
        }

        [Test]
        public void ShouldExchangeAsClientWithPooling()
        {
            // When connections are pooled (kept alive), we send HELLO and expect a PROCEED before each request, that way we can know whether
            // the connection was torn down first or not without attempting to transmit our request
            protocol.ExchangeAsClient(GetOutgoingRequestMessage());
            protocol.ExchangeAsClient(GetOutgoingRequestMessage());
            protocol.ExchangeAsClient(GetOutgoingRequestMessage());

            AssertOutput(@"
--> MX-CLIENT
<-- MX-SERVER
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope");
        }

        [Test]
        public void ShouldExchangeAsServerOfClientWithPooling()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(GetIncomingRequestMessage());
            stream.NextReadReturns(GetIncomingRequestMessage());
            stream.NextReadReturns(GetIncomingRequestMessage());

            protocol.ExchangeAsServer(req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), ri => new PendingRequestQueue(new InMemoryConnectionLog("x")));

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
<-- NEXT
--> PROCEED
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
<-- NEXT
--> PROCEED
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
<-- END");
        }

        [Test]
        public void ShouldExchangeAsSubscriber()
        {
            stream.NextReadReturns(GetIncomingRequestMessage());
            stream.NextReadReturns(GetIncomingRequestMessage());
            stream.NextReadReturns(GetIncomingRequestMessage());

            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), 5);

            AssertOutput(@"
--> MX-SUBSCRIBE subscriptionId
<-- MX-SERVER
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED");
        }

        [Test]
        public void ShouldExchangeAsServerOfSubscriber()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<MessageEnvelope>();
            queue.Enqueue(GetOutgoingRequestMessage());
            queue.Enqueue(GetOutgoingRequestMessage());
            requestQueue.Dequeue().Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServer(req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), ri => requestQueue);

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- NEXT
--> PROCEED
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- END");
        }

        [Test]
        public void ShouldExchangeAsServerOfSubscriberAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<MessageEnvelope>();
            queue.Enqueue(GetOutgoingRequestMessage());
            queue.Enqueue(GetOutgoingRequestMessage());
            requestQueue.DequeueAsync().Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServerAsync(req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), ri => requestQueue).Wait();

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- NEXT
--> PROCEED
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- END");
        }

        [Test]
        public void ShouldExchangeAsSubscriberWithPooling()
        {
            stream.NextReadReturns(GetIncomingRequestMessage());
            stream.NextReadReturns(GetIncomingRequestMessage());

            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), 5);

            stream.NextReadReturns(GetIncomingRequestMessage());

            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), 5);

            AssertOutput(@"
--> MX-SUBSCRIBE subscriptionId
<-- MX-SERVER
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> OutgoingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED
<-- IncomingMessageEnvelope
--> NEXT
<-- PROCEED");
        }

        [Test]
        public void ShouldExchangeAsServerOfSubscriberWithPooling()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<MessageEnvelope>();
            requestQueue.Dequeue().Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);

            queue.Enqueue(GetOutgoingRequestMessage());
            queue.Enqueue(GetOutgoingRequestMessage());
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServer(req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), ri => requestQueue);

            queue.Enqueue(GetOutgoingRequestMessage());

            stream.SetNumberOfReads(1);

            protocol.ExchangeAsServer(req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), ri => requestQueue);

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- NEXT
--> PROCEED
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- END
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- END");
        }


        [Test]
        public void ShouldExchangeAsServerOfSubscriberWithPoolingAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<MessageEnvelope>();
            requestQueue.DequeueAsync().Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);

            queue.Enqueue(GetOutgoingRequestMessage());
            queue.Enqueue(GetOutgoingRequestMessage());
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServerAsync(req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), ri => requestQueue).Wait();

            queue.Enqueue(GetOutgoingRequestMessage());

            stream.SetNumberOfReads(1);

            protocol.ExchangeAsServerAsync(req => MessageEnvelope.FromException(req, new Exception("Divide by zero")), ri => requestQueue).Wait();

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- NEXT
--> PROCEED
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- END
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> OutgoingMessageEnvelope
<-- IncomingMessageEnvelope
<-- END");
        }

        OutgoingMessageEnvelope GetOutgoingRequestMessage()
        {
            var id = Guid.NewGuid().ToString();
            return new OutgoingMessageEnvelope(id)
            {
                Message = new RequestMessage
                {
                    Id = id
                }
            };
        }

        IncomingMessageEnvelope GetIncomingRequestMessage()
        {
            var outgoingRequestMessage = GetOutgoingRequestMessage();
            return new IncomingMessageEnvelope
            {
                Id = outgoingRequestMessage.Id,
                InternalMessage = outgoingRequestMessage.ToBson()
            };
        }

        IncomingMessageEnvelope GetIncomingResponseMessage()
        {
            var outgoingResponseMessage = GetOutgoingResponseMessage();
            return new IncomingMessageEnvelope
            {
                Id = outgoingResponseMessage.Id,
                InternalMessage = outgoingResponseMessage.ToBson()
            };
        }

        OutgoingMessageEnvelope GetOutgoingResponseMessage()
        {
            var id = Guid.NewGuid().ToString();
            return new OutgoingMessageEnvelope(id)
            {
                Message = new ResponseMessage()
                {
                    Id = id
                }
            };
        }

        void AssertOutput(string expected)
        {
            Trace.WriteLine(stream.ToString());
            stream.ToString().Replace("\r\n", "\n").Trim().Should().Be(expected.Replace("\r\n", "\n").Trim());
        }

        class DumpStream : IMessageExchangeStream
        {
            readonly StringBuilder output = new StringBuilder();
            readonly Queue<object> nextReadQueue = new Queue<object>();
            RemoteIdentity remoteIdentity;
            int numberOfReads = 3;

            public DumpStream()
            {
                Sent = new List<object>();
            }

            public void NextReadReturns(object o)
            {
                nextReadQueue.Enqueue(o);
            }

            public void SetRemoteIdentity(RemoteIdentity identity)
            {
                remoteIdentity = identity;
            }

            public void SetNumberOfReads(int reads)
            {
                numberOfReads = reads;
            }

            public List<object> Sent { get; set; }

            public void IdentifyAsClient()
            {
                output.AppendLine("--> MX-CLIENT");
                output.AppendLine("<-- MX-SERVER");
            }

            public void SendNext()
            {
                output.AppendLine("--> NEXT");
            }

            public void SendProceed()
            {
                output.AppendLine("--> PROCEED");
            }

            public Task SendProceedAsync() => Task.Run(() => SendProceed());

            public void SendEnd()
            {
                output.AppendLine("--> END");
            }

            public bool ExpectNextOrEnd()
            {
                if (--numberOfReads == 0)
                {
                    output.AppendLine("<-- END");
                    return false;
                }
                output.AppendLine("<-- NEXT");
                return true;
            }

            public Task<bool> ExpectNextOrEndAsync() => Task.Run(() => ExpectNextOrEnd());

            public void ExpectProceeed()
            {
                output.AppendLine("<-- PROCEED");
            }

            public void IdentifyAsSubscriber(string subscriptionId)
            {
                output.AppendLine("--> MX-SUBSCRIBE subscriptionId");
                output.AppendLine("<-- MX-SERVER");
            }

            public void IdentifyAsServer()
            {
                output.AppendLine("--> MX-SERVER");
            }

            public RemoteIdentity ReadRemoteIdentity()
            {
                output.AppendLine("<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId");
                return remoteIdentity;
            }

            public void Send(MessageEnvelope message)
            {
                output.AppendLine("--> " + message.GetType().Name);
                Sent.Add(message);
            }

            public IncomingMessageEnvelope Receive()
            {
                output.AppendLine("<-- " + typeof(IncomingMessageEnvelope).Name);
                return (IncomingMessageEnvelope)(nextReadQueue.Count > 0 ? nextReadQueue.Dequeue() : new IncomingMessageEnvelope{Id = "EMPTY"});
            }

            public override string ToString()
            {
                return output.ToString();
            }
        }
    }
}