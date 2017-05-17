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
using Xunit;

namespace Halibut.Tests
{
    public class ProtocolFixture
    {
        MessageExchangeProtocol protocol;
        DumpStream stream;

        public ProtocolFixture()
        {
            stream = new DumpStream();
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Server));
            protocol = new MessageExchangeProtocol(stream);
        }

        [Fact]
        public void ShouldExchangeAsClient()
        {
            protocol.ExchangeAsClient(new RequestMessage());

            AssertOutput(@"
--> MX-CLIENT
<-- MX-SERVER
--> RequestMessage
<-- ResponseMessage");
        }

        [Fact]
        public void ShouldExchangeAsServerOfClient()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(new RequestMessage());
            stream.SetNumberOfReads(1);

            protocol.ExchangeAsServer(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => new PendingRequestQueue(new InMemoryConnectionLog("x")));

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
<-- RequestMessage
--> ResponseMessage
<-- END");
        }

        [Fact]
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

        [Fact]
        public void ShouldExchangeAsServerOfClientWithPooling()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());

            protocol.ExchangeAsServer(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => new PendingRequestQueue(new InMemoryConnectionLog("x")));

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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void ShouldExchangeAsServerOfSubscriberAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<RequestMessage>();
            queue.Enqueue(new RequestMessage());
            queue.Enqueue(new RequestMessage());
            requestQueue.DequeueAsync().Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServerAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue).Wait();

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

        [Fact]
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

        [Fact]
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


        [Fact]
        public void ShouldExchangeAsServerOfSubscriberWithPoolingAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<RequestMessage>();
            requestQueue.DequeueAsync().Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);

            queue.Enqueue(new RequestMessage());
            queue.Enqueue(new RequestMessage());
            stream.SetNumberOfReads(2);

            protocol.ExchangeAsServerAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue).Wait();

            queue.Enqueue(new RequestMessage());

            stream.SetNumberOfReads(1);

            protocol.ExchangeAsServerAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue).Wait();

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

            public List<object> Sent { get; set; } 

            public Task IdentifyAsClient()
            {
                output.AppendLine("--> MX-CLIENT");
                output.AppendLine("<-- MX-SERVER");

                return TaskEx.CompletedTask;
            }

            public Task SendNext()
            {
                output.AppendLine("--> NEXT");
                return TaskEx.CompletedTask;
            }

            public Task SendProceed()
            {
                output.AppendLine("--> PROCEED");
                return TaskEx.CompletedTask;
            }

            public Task<bool> ExpectNextOrEnd()
            {
                if (--numberOfReads == 0)
                {
                    output.AppendLine("<-- END");
                    return Task.FromResult(false);
                }
                output.AppendLine("<-- NEXT");
                return Task.FromResult(true);
            }

            public Task ExpectProceeed()
            {
                output.AppendLine("<-- PROCEED");
                return TaskEx.CompletedTask;
            }

            public Task IdentifyAsSubscriber(string subscriptionId)
            {
                output.AppendLine("--> MX-SUBSCRIBE subscriptionId");
                output.AppendLine("<-- MX-SERVER");
                return TaskEx.CompletedTask;
            }

            public Task IdentifyAsServer()
            {
                output.AppendLine("--> MX-SERVER");
                return TaskEx.CompletedTask;
            }

            public RemoteIdentity ReadRemoteIdentity()
            {
                output.AppendLine("<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId");
                return remoteIdentity;
            }

            public Task Send<T>(T message)
            {
                output.AppendLine("--> " + typeof(T).Name);
                Sent.Add(message);

                return TaskEx.CompletedTask;
            }

            public Task<T> Receive<T>()
            {
                output.AppendLine("<-- " + typeof(T).Name);     
                return Task.FromResult((T)(nextReadQueue.Count > 0 ? nextReadQueue.Dequeue() : default(T)));
            }

            public override string ToString()
            {
                return output.ToString();
            }
        }
    }
}