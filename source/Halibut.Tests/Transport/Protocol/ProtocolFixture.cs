using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class ProtocolFixture
    {
        MessageExchangeProtocol protocol = null!;
        DumpStream stream = null!;

        [SetUp]
        public void SetUp()
        {
            stream = new DumpStream();
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Server));
            protocol = new MessageExchangeProtocol(stream, Substitute.For<IRpcObserver>(), Substitute.For<ILog>());
        }

        // TODO - ASYNC ME UP! ExchangeAsClientAsync cancellation

        [Test]
        public async Task ShouldExchangeAsClient()
        {
            await protocol.ExchangeAsClientAsync(new RequestMessage(), CancellationToken.None);

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

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => new PendingRequestQueueAsync(new HalibutTimeoutsAndLimitsForTestsBuilder().Build(), new InMemoryConnectionLog("x")), CancellationToken.None);

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
<-- RequestMessage
--> ResponseMessage
<-- END");
        }

        [Test]
        public async Task ShouldExchangeAsClientWithPooling()
        {
            // When connections are pooled (kept alive), we send HELLO and expect a PROCEED before each request, that way we can know whether
            // the connection was torn down first or not without attempting to transmit our request
            
            await protocol.ExchangeAsClientAsync(new RequestMessage(), CancellationToken.None);
            await protocol.ExchangeAsClientAsync(new RequestMessage(), CancellationToken.None);
            await protocol.ExchangeAsClientAsync(new RequestMessage(), CancellationToken.None);

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

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => new PendingRequestQueueAsync(new HalibutTimeoutsAndLimitsForTestsBuilder().Build(), new InMemoryConnectionLog("x")), CancellationToken.None);

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
        public async Task ShouldExchangeAsSubscriber()
        {
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());

            await protocol.ExchangeAsSubscriberAsync(new Uri("poll://12831"), req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), 5, CancellationToken.None);
            
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

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, CancellationToken.None);

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
        public async Task ShouldExchangeAsSubscriberWithPooling()
        {
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            
            await protocol.ExchangeAsSubscriberAsync(new Uri("poll://12831"), req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), 5, CancellationToken.None);
            stream.NextReadReturns(new RequestMessage());
            await protocol.ExchangeAsSubscriberAsync(new Uri("poll://12831"), req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), 5, CancellationToken.None);

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
        public async Task ShouldExchangeAsServerOfSubscriberWithPoolingAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<RequestMessage>();
            requestQueue.DequeueAsync(CancellationToken.None).Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);

            queue.Enqueue(new RequestMessage());
            queue.Enqueue(new RequestMessage());
            stream.SetNumberOfReads(2);

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, CancellationToken.None);

            queue.Enqueue(new RequestMessage());

            stream.SetNumberOfReads(1);

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, CancellationToken.None);

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
            readonly StringBuilder output = new();
            readonly Queue<object> nextReadQueue = new();
            RemoteIdentity remoteIdentity;
            int numberOfReads = 3;

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

            public void IdentifyAsClient()
            {
                output.AppendLine("--> MX-CLIENT");
                output.AppendLine("<-- MX-SERVER");
            }

            public async Task IdentifyAsClientAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                IdentifyAsClient();
            }
            
            public async Task SendNextAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("--> NEXT");
            }
            
            public async Task SendProceedAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("--> PROCEED");
            }

            public async Task SendEndAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("--> END");
            }

            public async Task<bool> ExpectNextOrEndAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                if (--numberOfReads == 0)
                {
                    output.AppendLine("<-- END");
                    return false;
                }
                output.AppendLine("<-- NEXT");
                return true;
            }

            public async Task ExpectProceedAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("<-- PROCEED");
            }
            
            public async Task IdentifyAsSubscriberAsync(string subscriptionId, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("--> MX-SUBSCRIBE subscriptionId");
                output.AppendLine("<-- MX-SERVER");
            }
            
            public async Task IdentifyAsServerAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("--> MX-SERVER");
            }
            
            public async Task<RemoteIdentity> ReadRemoteIdentityAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId");
                return remoteIdentity;
            }
            
            public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("--> " + typeof(T).Name);
            }
            
            public async Task<T> ReceiveAsync<T>(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

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
