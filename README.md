## Halibut

Halibut is a secure, JSON RPC over TLS-based client/server communication framework for .NET. 

Features:

 - No dependency on HTTP.sys - simply uses `TcpListener` and `SslStream`
 - Uses Json.NET for serialization of RPC calls following [JSON-RPC](http://json-rpc.org/)
 - Client/server logs can be viewed using the WCF Service Trace Viewer
 - Runs on both Mono and .NET

Halibut is currently just an experiment, and may someday replace WCF `wsHttpBinding` as the communication stack for [Octopus Deploy](http://octopusdeploy.com/). 

### Protocol and security

Halibut uses the .NET Framework `SslStream` class, using the [TLS 1.0](http://msdn.microsoft.com/en-us/library/system.security.authentication.sslprotocols.aspx) protocol. The client and server must both provide a certificate, and they can choose to validate each other's certificates.

Once the SSL connection is established, Halibut sends JSON-RPC requests and responses using Json.NET, encoded using the Json.NET `BsonReader`/`BsonWriter`. 
