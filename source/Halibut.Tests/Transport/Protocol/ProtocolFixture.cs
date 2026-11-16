using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Transport;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class ProtocolFixture
    {
        const string ClientThumbprint = "36F35047CE8B000CF4C671819A2DD1AFCDE3403D";
        const string OtherClientThumbprint = "76225C0717A16C1D0BA4A7FFA76519D286D8A248";
        static readonly Uri SubscriptionId = new("poll://12831");

        MessageExchangeProtocol protocol = null!;
        DumpStream stream = null!;

        [SetUp]
        public void SetUp()
        {
            stream = new DumpStream();
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Server));
            var limits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            var activeConnectionsLimiter = new ActiveTcpConnectionsLimiter(limits, NoOpConnectionsObserver.Instance);
            protocol = new MessageExchangeProtocol(stream, new HalibutTimeoutsAndLimitsForTestsBuilder().Build(), activeConnectionsLimiter, Substitute.For<ILog>());
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

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => new PendingRequestQueueAsync(new HalibutTimeoutsAndLimitsForTestsBuilder().Build(), new InMemoryConnectionLog("x")), ClientThumbprint, ri => Substitute.For<ILog>(), CancellationToken.None);

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

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => new PendingRequestQueueAsync(new HalibutTimeoutsAndLimitsForTestsBuilder().Build(), new InMemoryConnectionLog("x")), ClientThumbprint, ri => Substitute.For<ILog>(), CancellationToken.None);

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

            var wasIdentified = false;

            await protocol.ExchangeAsSubscriberAsync(
                new Uri("poll://12831"),
                req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))),
                5,
                () => wasIdentified = true,
                CancellationToken.None);

            wasIdentified.Should().BeTrue();

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
            var queue = new Queue<RequestMessageWithCancellationToken>();
            queue.Enqueue(new(RequestForSubscriber(), CancellationToken.None));
            queue.Enqueue(new(RequestForSubscriber(), CancellationToken.None));
            requestQueue.DequeueAsync(CancellationToken.None).Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);
            stream.SetNumberOfReads(2);

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, ClientThumbprint, ri => Substitute.For<ILog>(), CancellationToken.None);

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

            var wasIdentified = false;
            var wasIdentifiedCount = 0;

            Action wasIdentifiedCallback = () =>
            {
                wasIdentified = true;
                wasIdentifiedCount++;
            };

            await protocol.ExchangeAsSubscriberAsync(new Uri("poll://12831"), req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), 5, wasIdentifiedCallback, CancellationToken.None);
            stream.NextReadReturns(new RequestMessage());
            await protocol.ExchangeAsSubscriberAsync(new Uri("poll://12831"), req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), 5, wasIdentifiedCallback, CancellationToken.None);

            wasIdentified.Should().BeTrue();
            wasIdentifiedCount.Should().Be(2, "pooling does not affect identification checks");
            
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
            var queue = new Queue<RequestMessageWithCancellationToken>();
            requestQueue.DequeueAsync(CancellationToken.None).Returns(ci => queue.Count > 0 ? queue.Dequeue() : null);

            queue.Enqueue(new(RequestForSubscriber(), CancellationToken.None));
            queue.Enqueue(new(RequestForSubscriber(), CancellationToken.None));
            stream.SetNumberOfReads(2);

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, ClientThumbprint, ri => Substitute.For<ILog>(), CancellationToken.None);

            queue.Enqueue(new(RequestForSubscriber(), CancellationToken.None));

            stream.SetNumberOfReads(1);

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, ClientThumbprint, ri => Substitute.For<ILog>(), CancellationToken.None);

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
        public async Task ShouldNotSendRequestToSubscriberThatPresentedADifferentCertificate()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, SubscriptionId));
            var request = RequestForSubscriber(OtherClientThumbprint);
            var (requestQueue, appliedResponses) = QueueReturning(request, RequestForSubscriber());
            var subscriberLog = Substitute.For<ILog>();

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, ClientThumbprint, ri => subscriberLog, CancellationToken.None);

            // The request is never sent, and the connection is closed before any other request is collected.
            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER");
            await requestQueue.Received(1).DequeueAsync(Arg.Any<CancellationToken>());

            appliedResponses.Should().ContainSingle();
            var response = appliedResponses[0];
            response.Id.Should().Be(request.Id);
            response.Error!.HalibutErrorType.Should().Be(typeof(PollingClientCertificateMismatchHalibutClientException).FullName);
            response.Error.Message.Should().Contain(ClientThumbprint).And.Contain(OtherClientThumbprint).And.Contain(SubscriptionId.ToString());

            subscriberLog.Received(1).Write(EventType.ErrorInIdentify, Arg.Is<string>(m => m.Contains(ClientThumbprint) && m.Contains(OtherClientThumbprint)));
        }

        [Test]
        public async Task ShouldNotSendRequestToSubscriberWhenTheDestinationDoesNotSpecifyAThumbprint()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, SubscriptionId));
            var (requestQueue, appliedResponses) = QueueReturning(RequestForSubscriber(thumbprint: null));

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, ClientThumbprint, ri => Substitute.For<ILog>(), CancellationToken.None);

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER");
            appliedResponses.Should().ContainSingle()
                .Which.Error!.HalibutErrorType.Should().Be(typeof(PollingClientCertificateMismatchHalibutClientException).FullName);
        }

        [Test]
        public async Task ShouldMatchSubscriberCertificateThumbprintIgnoringCase()
        {
            stream.SetRemoteIdentity(new RemoteIdentity(RemoteIdentityType.Subscriber, SubscriptionId));
            var request = RequestForSubscriber(ClientThumbprint.ToLowerInvariant());
            var (requestQueue, appliedResponses) = QueueReturning(request);
            stream.NextReadReturns(ResponseMessage.FromResult(request, "Hello"));
            stream.SetNumberOfReads(1);

            await protocol.ExchangeAsServerAsync(req => Task.FromResult(ResponseMessage.FromException(req, new Exception("Divide by zero"))), ri => requestQueue, ClientThumbprint, ri => Substitute.For<ILog>(), CancellationToken.None);

            AssertOutput(@"
<-- MX-CLIENT || MX-SUBSCRIBE subscriptionId
--> MX-SERVER
--> RequestMessage
<-- ResponseMessage
<-- END");
            appliedResponses.Should().ContainSingle().Which.Result.Should().Be("Hello");
        }

        static RequestMessage RequestForSubscriber(string? thumbprint = ClientThumbprint)
        {
            return new RequestMessage
            {
                Id = Guid.NewGuid().ToString(),
                ActivityId = Guid.NewGuid(),
                ServiceName = "IEchoService",
                MethodName = "SayHello",
                Destination = new ServiceEndPoint(SubscriptionId, thumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
            };
        }

        static (IPendingRequestQueue Queue, List<ResponseMessage> AppliedResponses) QueueReturning(params RequestMessage[] requests)
        {
            var queue = new Queue<RequestMessageWithCancellationToken>();
            foreach (var request in requests)
            {
                queue.Enqueue(new(request, CancellationToken.None));
            }

            var appliedResponses = new List<ResponseMessage>();
            var requestQueue = Substitute.For<IPendingRequestQueue>();
            requestQueue.DequeueAsync(Arg.Any<CancellationToken>()).Returns(_ => queue.Count > 0 ? queue.Dequeue() : null);
            requestQueue.ApplyResponse(Arg.Do<ResponseMessage>(appliedResponses.Add), Arg.Any<Guid>()).Returns(Task.CompletedTask);
            return (requestQueue, appliedResponses);
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
            RemoteIdentity? remoteIdentity;
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

            public async Task<bool> ExpectNextOrEndAsync(TimeSpan readTimeout, CancellationToken cancellationToken)
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
                return remoteIdentity!;
            }

            public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;

                output.AppendLine("--> " + typeof(T).Name);
            }

            public Task<RequestMessage?> ReceiveRequestAsync(TimeSpan timeoutForReceivingTheFirstByte, CancellationToken cancellationToken)
            {
                return ReceiveAsync<RequestMessage>();
            }

            public Task<ResponseMessage?> ReceiveResponseAsync(CancellationToken cancellationToken)
            {
                return ReceiveAsync<ResponseMessage>();
            }

            async Task<T?> ReceiveAsync<T>()
            {
                await Task.CompletedTask;

                output.AppendLine("<-- " + typeof(T).Name);
                return (T?)(nextReadQueue.Count > 0 ? nextReadQueue.Dequeue() : default(T));
            }

            public override string ToString()
            {
                return output.ToString();
            }
        }
    }
}