Halibut is the communication framework behind Octopus Deploy 3.0. 

## Overview

Like WCF and other RPC-based communication frameworks, Halibut uses a simple request/response based programming model. However, unlike other request/response frameworks, the transport layer can be configured to allow either party to be a TCP listener or TCP client. 

![Halibut](http://res.cloudinary.com/octopusdeploy/image/upload/v1421035742/halibut_rqxrw2.png)

To understand the difference, consider WCF using `wsHttpBinding`. The WCF "client" is always a TCP client, and the WCF "service" is always a TCP listener. For a client to send a request to a server, TCP ports must be opened on the server. This is not always possible. 

In Halibut, the relationship between the *logical* request/response client/service, and the *underlying* TCP client/listener, is decoupled. The Halibut client might in fact be a TCP listener, while the Halibut service is a TCP client, polling the TCP listener for requests to process. 

For Octopus, this means that customers can configure the Tentacle (which hosts services that the Octopus client connects to) in either listening or polling mode. 

Halibut has the following features:

 - A simple, request/response based programming model ([why we prefer this over messaging](http://octopusdeploy.com/blog/actors-vs-rpc-in-octopus-3))
 - Connections in either direction are secured using SSL, with server and client certificates to provide authentication
 - No dependency on HTTP.sys - simply uses `TcpListener`, `TcpClient` and `SslStream`
 - Requests/responses are serialized using Json.NET BSON, and GZipped
 - Requests and responses can also contain streams of arbitrary length


A more detailed look at the protocol can be found under the [Halibit Protocol](docs/halibut-protocol.md) page.

## Usage

Clients and servers both make use of `HalibutRuntime` to distribute messages. In this example, there's a "Tentacle" that listens on a port, and Octopus connects to it:

```csharp
using (var octopus = new HalibutRuntime(services, Certificates.Bob))
using (var tentacleListening = new HalibutRuntime(services, Certificates.Alice))
{
    tentacleListening.Listen(8014);
    tentacleListening.Trust(Certificates.BobPublicThumbprint);

    var calculator = octopus.CreateClient<ICalculatorService>(new ServiceEndPoint(new Uri("https://localhost:8014"), Certificates.AlicePublicThumbprint));
    var three = calculator.Add(1, 2);
    Assert.That(three, Is.EqualTo(3));
}
```

Alternatively, here's a mode where Octopus listens, and Tentacle polls it:

```csharp
using (var octopus = new HalibutRuntime(services, Certificates.Bob))
using (var tentaclePolling = new HalibutRuntime(services, Certificates.Alice))
{
    octopus.Listen(8013);
    tentaclePolling.Poll(new Uri("poll://subscription123"), new ServiceEndPoint(new Uri("https://localhost:8013"), Certificates.BobPublicThumbprint));

    var calculator = octopus.CreateClient<ICalculatorService>(new ServiceEndPoint(new Uri("poll://subscription123"), Certificates.AlicePublicThumbprint));
    var three = calculator.Add(1, 2);
    Assert.That(three, Is.EqualTo(3));
}
```

Notice that while the configuration code changed, the request/response code didn't apart from the endpoint. Logically, the Octopus is still the request/response client, and the Tentacle is still the request/response server, even though the transport layer has Octopus as the TCP listener and Tentacle as the TCP client polling for work.

## RPC over Redis

Halibut supports executing RPC commands between nodes using a shared Redis queue as the communication mechanism. In this mode, nodes do not communicate directly with each other - all communication flows through Redis. This is particularly useful for scenarios where multiple nodes can all access a shared Redis instance but cannot establish direct network connections to each other.

### How it works

When using RPC over Redis:

- The client queues RPC requests in Redis
- The server polls Redis for pending requests
- The server processes requests and writes responses back to Redis
- The client retrieves responses from Redis

This decoupled communication model allows nodes behind firewalls, in different networks, or with restricted connectivity to communicate as long as they can all reach the shared Redis instance.

### Usage

See the [SimpleLocalExecutionExample](source/Halibut.Tests/LocalExecutionModeFixture.cs) test for a complete example of how to set up and use RPC over Redis.

For more detailed information about the Redis queue implementation, refer to the [Redis Queue documentation](docs/RedisQueue.md).

## Failure modes

One area we've put a lot of thought into with Halibut is failure modes. Below is a list of possible failure reasons, and how Halibut will handle them. 

 - We cannot connect (invalid host name, port blocked, etc.): we try up to 5 times, and for no longer than 30 seconds
 - We connect, but the connection is torn down: no retry
 - We connect, but the server rejects our certificate: no retry
 - We connect, but the server certificate is not what we expect: no retry
 - We connect, but the server encounters an error processing a given request: error is returned and rethrown, no retry
 - We connect, but we encounter an error processing a server request: no retry, error is returned
 - Sending a message to a polling endpoint, but the endpoint doesn't collect the message in a reasonable time (30 seconds currently): fail
