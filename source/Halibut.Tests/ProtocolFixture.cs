using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
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
            protocol.ExchangeAsClient(new RequestMessage());

            AssertOutput(@"
--> MX-CLIENT
<-- MX-SERVER
--> RequestMessage
<-- ResponseMessage");
        }

        [Test]
        public void ShouldExchangeAsServerOfClient()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(new RequestMessage());
            stream.SetNumberOfReads(1);

            protocol.ExchangeAsServer(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => new PendingRequestQueue());

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
<-- RequestMessage
--> ResponseMessage
<-- END");
        }

        [Test]
        public void ShouldExchangeAsClientWithPooling()
        {
            // When connections are pooled (kept alive), we send HELLO and expect a PROCEED before each request, that way we can know whether
            // the connection was torn down first or not without attempting to transmit our request
            protocol.ExchangeAsClient(new RequestMessage());
            protocol.ExchangeAsClient(new RequestMessage());
            protocol.ExchangeAsClient(new RequestMessage());

            AssertOutput(@"
--> MX-CLIENT
<-- MX-SERVER
--> RequestMessage
<-- ResponseMessage
--> NEXT
<-- PROCEED
--> RequestMessage
<-- ResponseMessage
--> NEXT
<-- PROCEED
--> RequestMessage
<-- ResponseMessage");
        }

        [Test]
        public void ShouldExchangeAsServerOfClientWithPooling()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());

            protocol.ExchangeAsServer(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => new PendingRequestQueue());

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
<-- RequestMessage
--> ResponseMessage
<-- NEXT
--> PROCEED
<-- RequestMessage
--> ResponseMessage
<-- NEXT
--> PROCEED
<-- RequestMessage
--> ResponseMessage
<-- END");
        }

        [Test]
        public void ShouldExchangeAsSubscriber()
        {
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            
            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")));
            
            AssertOutput(@"
--> MX-SUBSCRIBE subscriptionId
<-- MX-SERVER
<-- RequestMessage
--> ResponseMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> ResponseMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> ResponseMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> NEXT
<-- PROCEED");
        }

        [Test]
        public void ShouldExchangeAsServerOfSubscriber()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<RequestMessage>();
            queue.Enqueue(new RequestMessage());
            queue.Enqueue(new RequestMessage());
            requestQueue.Dequeue().Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServer(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue);

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> RequestMessage
<-- ResponseMessage
<-- NEXT
--> PROCEED
--> RequestMessage
<-- ResponseMessage
<-- END");
        }

        [Test]
        public void ShouldExchangeAsSubscriberWithPooling()
        {
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());

            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")));

            stream.NextReadReturns(new RequestMessage());

            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")));

            AssertOutput(@"
--> MX-SUBSCRIBE subscriptionId
<-- MX-SERVER
<-- RequestMessage
--> ResponseMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> ResponseMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> ResponseMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> NEXT
<-- PROCEED");
        }

        [Test]
        public void ShouldExchangeAsServerOfSubscriberWithPooling()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<RequestMessage>();
            requestQueue.Dequeue().Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);

            queue.Enqueue(new RequestMessage());
            queue.Enqueue(new RequestMessage());
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServer(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue);

            queue.Enqueue(new RequestMessage());

            stream.SetNumberOfReads(1);

            protocol.ExchangeAsServer(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue);

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> RequestMessage
<-- ResponseMessage
<-- NEXT
--> PROCEED
--> RequestMessage
<-- ResponseMessage
<-- END
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> RequestMessage
<-- ResponseMessage
<-- END");
        }

        void AssertOutput(string expected)
        {
            Trace.WriteLine(stream.ToString());
            Assert.That(stream.ToString().Replace("\r\n", "\n").Trim(), Is.EqualTo(expected.Replace("\r\n", "\n").Trim()));
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

            public void Send<T>(T message)
            {
                output.AppendLine("--> " + typeof(T).Name);
                Sent.Add(message);
            }

            public T Receive<T>()
            {
                output.AppendLine("<-- " + typeof(T).Name);     
                return (T) (nextReadQueue.Count > 0 ? nextReadQueue.Dequeue() : default(T));
            }

            public override string ToString()
            {
                return output.ToString();
            }
        }
    }
}