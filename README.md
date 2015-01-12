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
