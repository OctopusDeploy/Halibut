using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
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
            protocol = new MessageExchangeProtocol(stream, Substitute.For<ILog>());
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
        public async Task ShouldExchangeAsServerOfClientAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(new RequestMessage());
            stream.SetNumberOfReads(1);

            await protocol.ExchangeAsServerSynchronouslyAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => new PendingRequestQueue(new InMemoryConnectionLog("x")));

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
        public async Task ShouldExchangeAsServerOfClientWithPoolingAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());

            await protocol.ExchangeAsServerSynchronouslyAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => new PendingRequestQueue(new InMemoryConnectionLog("x")));

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

            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")), 5);

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
<-- PROCEED
<-- RequestMessage
--> NEXT
<-- PROCEED");
        }

        [Test]
        public async Task ShouldExchangeAsServerOfSubscriberAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<RequestMessage>();
            queue.Enqueue(new RequestMessage());
            queue.Enqueue(new RequestMessage());
            requestQueue.DequeueAsync(CancellationToken.None).Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);
            stream.SetNumberOfReads(2);

            await protocol.ExchangeAsServerSynchronouslyAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue);

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

            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")), 5);

            stream.NextReadReturns(new RequestMessage());

            protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")), 5);

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
<-- PROCEED
<-- RequestMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> NEXT
<-- PROCEED
<-- RequestMessage
--> NEXT
<-- PROCEED");
        }
        
        [Test]
        public void ShouldExchangeAsServerOfSubscriberWithPoolingAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<RequestMessage>();
            requestQueue.DequeueAsync(CancellationToken.None).Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);

            queue.Enqueue(new RequestMessage());
            queue.Enqueue(new RequestMessage());
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServerSynchronouslyAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue).Wait();

            queue.Enqueue(new RequestMessage());

            stream.SetNumberOfReads(1);

            protocol.ExchangeAsServerSynchronouslyAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue).Wait();

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

            public List<object> Sent { get; }

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

            public void Send<T>(T message)
            {
                output.AppendLine("--> " + typeof(T).Name);
                Sent.Add(message);
            }

            public T Receive<T>()
            {
                output.AppendLine("<-- " + typeof(T).Name);
                return (T)(nextReadQueue.Count > 0 ? nextReadQueue.Dequeue() : default(T));
            }

            public override string ToString()
            {
                return output.ToString();
            }
        }
    }
}