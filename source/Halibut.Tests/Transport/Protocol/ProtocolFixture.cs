using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.Support.TestAttributes;
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
            protocol = new MessageExchangeProtocol(stream, Substitute.For<ILog>());
        }

        // TODO - ASYNC ME UP! ExchangeAsClientAsync cancellation

        [Test]
        [SyncAndAsync]
        public async Task ShouldExchangeAsClient(SyncOrAsync syncOrAsync)
        {
#pragma warning disable CS0612
            await syncOrAsync
                .WhenSync(() => protocol.ExchangeAsClient(new RequestMessage()))
                .WhenAsync(async () => await protocol.ExchangeAsClientAsync(new RequestMessage(), CancellationToken.None));
#pragma warning restore CS0612

            AssertOutput(@"
--> MX-CLIENT
<-- MX-SERVER
--> RequestMessage
<-- ResponseMessage");
        }

        [Test]
        [Obsolete]
        public async Task ShouldExchangeAsServerOfClientSynchronouslyAsync()
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
        public async Task ShouldExchangeAsServerOfClientAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(new RequestMessage());
            stream.SetNumberOfReads(1);

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => new PendingRequestQueue(new InMemoryConnectionLog("x")), CancellationToken.None);

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
<-- RequestMessage
--> ResponseMessage
<-- END");
        }

        [Test]
        [SyncAndAsync]
        public async Task ShouldExchangeAsClientWithPooling(SyncOrAsync syncOrAsync)
        {
            // When connections are pooled (kept alive), we send HELLO and expect a PROCEED before each request, that way we can know whether
            // the connection was torn down first or not without attempting to transmit our request
            
            await syncOrAsync
                .WhenSync(() =>
                {
#pragma warning disable CS0612
                    // ReSharper disable once MethodHasAsyncOverload
                    protocol.ExchangeAsClient(new RequestMessage());
                    protocol.ExchangeAsClient(new RequestMessage());
                    protocol.ExchangeAsClient(new RequestMessage());
#pragma warning restore CS0612
                })
                .WhenAsync(async () =>
                {
                    await protocol.ExchangeAsClientAsync(new RequestMessage(), CancellationToken.None);
                    await protocol.ExchangeAsClientAsync(new RequestMessage(), CancellationToken.None);
                    await protocol.ExchangeAsClientAsync(new RequestMessage(), CancellationToken.None);
                });


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
        [Obsolete]
        public async Task ShouldExchangeAsServerOfClientWithPoolingSynchronouslyAsync()
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
        public async Task ShouldExchangeAsServerOfClientWithPoolingAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Client));
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => new PendingRequestQueue(new InMemoryConnectionLog("x")), CancellationToken.None);

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
        [SyncAndAsync]
        public async Task ShouldExchangeAsSubscriber(SyncOrAsync syncOrAsync)
        {
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());

#pragma warning disable CS0612
            await syncOrAsync
                .WhenSync(() => protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")), 5))
                .WhenAsync(async () => await protocol.ExchangeAsSubscriberAsync(new Uri("poll://12831"), req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), 5, CancellationToken.None));
#pragma warning restore CS0612
            
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
        [Obsolete]
        public async Task ShouldExchangeAsServerOfSubscriberSynchronouslyAsync()
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
        [SyncAndAsync]
        public async Task ShouldExchangeAsSubscriberWithPooling(SyncOrAsync syncOrAsync)
        {
            stream.NextReadReturns(new RequestMessage());
            stream.NextReadReturns(new RequestMessage());

            await syncOrAsync
                .WhenSync(() =>
                {
#pragma warning disable CS0612
                    protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")), 5);
                    stream.NextReadReturns(new RequestMessage());
                    protocol.ExchangeAsSubscriber(new Uri("poll://12831"), req => ResponseMessage.FromException(req, new Exception("Divide by zero")), 5);
#pragma warning restore CS0612
                })
                .WhenAsync(async () =>
                {
                    await protocol.ExchangeAsSubscriberAsync(new Uri("poll://12831"), req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), 5, CancellationToken.None);
                    stream.NextReadReturns(new RequestMessage());
                    await protocol.ExchangeAsSubscriberAsync(new Uri("poll://12831"), req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), 5, CancellationToken.None);
                });

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
        [Obsolete]
        public async Task ShouldExchangeAsServerOfSubscriberWithPoolingSynchronouslyAsync()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, new Uri("poll://12831")));
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            var queue = new Queue<RequestMessage>();
            requestQueue.DequeueAsync(CancellationToken.None).Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);

            queue.Enqueue(new RequestMessage());
            queue.Enqueue(new RequestMessage());
            stream.SetNumberOfReads(2);

            await protocol.ExchangeAsServerSynchronouslyAsync(req => ResponseMessage.FromException(req, new Exception("Divide by zero")), ri => requestQueue);

            queue.Enqueue(new RequestMessage());

            stream.SetNumberOfReads(1);

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
<-- END
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> RequestMessage
<-- ResponseMessage
<-- END");
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

            [Obsolete]
            public void SendNext()
            {
                output.AppendLine("--> NEXT");
            }

            public async Task SendNextAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                SendNext();
#pragma warning restore CS0612
            }

            [Obsolete]
            public void SendProceed()
            {
                output.AppendLine("--> PROCEED");
            }

            [Obsolete]
            public async Task SendProceedAsync()
            {
                await Task.CompletedTask;

                SendProceed();
            }

            public async Task SendProceedAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                SendProceed();
#pragma warning restore CS0612
            }

            [Obsolete]
            public void SendEnd()
            {
                output.AppendLine("--> END");
            }

            public async Task SendEndAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                SendEnd();
#pragma warning restore CS0612
            }

            [Obsolete]
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

            [Obsolete]
            public async Task<bool> ExpectNextOrEndAsync()
            {
                await Task.CompletedTask;

                return ExpectNextOrEnd();
            }

            public async Task<bool> ExpectNextOrEndAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                return ExpectNextOrEnd();
#pragma warning restore CS0612
            }

            [Obsolete]
            public void ExpectProceeed()
            {
                output.AppendLine("<-- PROCEED");
            }

            public async Task ExpectProceedAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                ExpectProceeed();
#pragma warning restore CS0612
            }

            [Obsolete]
            public void IdentifyAsSubscriber(string subscriptionId)
            {
                output.AppendLine("--> MX-SUBSCRIBE subscriptionId");
                output.AppendLine("<-- MX-SERVER");
            }

            public async Task IdentifyAsSubscriberAsync(string subscriptionId, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                IdentifyAsSubscriber(subscriptionId);
#pragma warning restore CS0612
            }

            [Obsolete]
            public void IdentifyAsServer()
            {
                output.AppendLine("--> MX-SERVER");
            }

            public async Task IdentifyAsServerAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                IdentifyAsServer();
#pragma warning restore CS0612
            }

            [Obsolete]
            public RemoteIdentity ReadRemoteIdentity()
            {
                output.AppendLine("<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId");
                return remoteIdentity;
            }

            public async Task<RemoteIdentity> ReadRemoteIdentityAsync(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                return ReadRemoteIdentity();
#pragma warning restore CS0612
            }

            [Obsolete]
            public void Send<T>(T message)
            {
                output.AppendLine("--> " + typeof(T).Name);
            }

            public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                Send(message);
#pragma warning restore CS0612
            }

            [Obsolete]
            public T Receive<T>()
            {
                output.AppendLine("<-- " + typeof(T).Name);
                return (T)(nextReadQueue.Count > 0 ? nextReadQueue.Dequeue() : default(T));
            }

            public async Task<T> ReceiveAsync<T>(CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

#pragma warning disable CS0612
                return Receive<T>();
#pragma warning restore CS0612
            }

            public override string ToString()
            {
                return output.ToString();
            }
        }
    }
}
