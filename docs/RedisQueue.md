# Redis Pending Request Queue Beta

Halibut provides a Redis backed pending request queue for multi node setups. This solves the problem where 
a cluster of multiple clients need to send commands to polling services which connect to only one of the
clients. 

For example if we have two clients ClientA and ClientB and the Service connects to B, yet A wants
to execute an RPC. Currently that won't work as the request will end up in the in memory queue for ClientA
but it needs to be accessible to ClientB.

The Redis queue solves this, as the request is placed into Redis allowing ClientB to access the request and
so send it to the Service.

## How to run Redis for this queue.

Redis can be started by running the following command in the root of the directory:

```
docker run -v `pwd`/redis-conf:/usr/local/etc/redis -p 6379:6379 --name redis -d redis redis-server /usr/local/etc/redis/redis.conf
```

Note that Redis is configured to have no backup, everything must be in memory. The queue makes this assumption to function.

## TODO design.

### Context: Pending Request Queue.

Halibut turns an RPC call into a RequestMessage which is placed into the Pending Request Queue. This is done by calling: `ResponseMessage QueueAndWait(RequestMessage)`. Which is a blocking call that queues the RequestMessage and waits for the ResponseMessage before returning.

Polling service, e.g, Tentacle, call into the `Dequeue` method of the queue to get the next `RequestMessage` to processing. It then responds by calling `ApplyResponse(ResponseMessage)`, doing so results in `QueueAndWait()` returning the ResponseMessage. This in turn results in the RPC call completing.

The Redis Pending Request Queue solves the problem where we have multiple clients, that wish to execute RPC calls to a single Polling Service that is connected to exactly one client. For example Client A makes an RPC call, but the service is connected to Client B. The Redis Pending Request Queue is what moves the `RequestMessage` from Client A to Client B to be sent to the service.

### Context: Redis

First we need to understand just a little about Redis and how we are using redis:
 - Redis may have data lose.
 - Pub/Sub does not have guaranteed delivery, we can miss publication.
 - Pub/Sub channels are not pets in Redis, they can be created simply by "subscribing" and are "deleted" when there are no subscribers to that channel. 
 - Redis is connected to via the network, which can be flaky we will make retries to Redis when we can.

## High Level design.

Setup: 
 - Client A is executing the RPC call
 - Client B  has the Polling service connected to it.

At a high level steps the Redis Queue goes through to execute an RPC are:

 1. Client B subscribes to the unique "RequestMessage Pulse Channel", as the client service is connected to it. The channel is keyed by the polling client id e.g. "poll://123"
 2. Client A executes an RPC and so Calls QueueAndWait with a RequestMessage. Each RequestMessage has a unique `GUID`.
 2.1 Client A subscribes to the `ResponseMessage channel` keyed by `GUID` to be notified when a response is available.
 3. Client A serialises the message and places the message into a hash in Redis keyed by the RequestMessage `Guid`.
 4. Client A Adds the `GUID` to the polling clients unique Redis list (aka queue). The key is the polling client id e.g. "poll://123".
 5. Client A pulses the polling clients unique "RequestMessage Pulse Channel", to alert to it that it has work to do.
 6. Client B receives the Pulse message and tries to dequeue a `GUID` from the polling clients unique Redis list (aka queue).
 7. Client B now has the `GUID` of the request and so atomically gets and deletes the RequestMessage from the Redis Hash using that guid.
 8. Client B sends the request to the tentacle, waits for the response, and calls `ApplyResponse()` with the ResponseMessage.
 9. Client B writes the `ResponseMessage` to redis in a hash using the `GUID` as the key.
 10. Client B Pulses the `ResponseMessage channel` keyed by the RequestMessage `GUID`, that a Response is available.
 11. Client A receives a pulse on the `ResponseMessage channel` and so knows a Response is available, it reads the response from Redis and returns from the `QueueAndWait()` method.    

## Cancellation support.

The Redis PRQ supports cancellation, even for collected requests. This is done by the RequestReceiverNode (ie the node connected to the Service) subscribing to the request cancellation channel and polling for request cancellation.

## Dealing with minor network interruptions to Redis.

All operations to redis are retried for up to 30s, this allows connections to Redis to go down briefly with impacting RPCs even for non idempotent RPCs.

###  Pub/Sub and Poll.

Since Pub/Sub does not have guaranteed delivery in Redis, in any place that we do Pub/Sub we must also have a form of polling. For example:
 - When Dequeuing work not only are we subscribed but when `Dequeue()` is called we also check for work on the queue anyway. (Note that Dequeue() returns every 30s if there is no work, and thus we have polling.)
 - When waiting for a Response, we are not only subscribed to the response channel we also poll to see if the Response has been sent back.

## Dealing with nodes that disappear mid request.

Either node could go offline at any time, including during execution of an RPC. For example:
 - The node executing the RPC could go offline, when the node with the Service connected is sending the Request to the Service.
 - The node sending the Request to the Service could go offline.

To handle this case in a way that allows for large file transfers aka request that take a long time, we have a concept of "heart beats".

When executing an RPC both nodes involved will send heart beats to a unique channel keyed by the request ID AND the nodes role in the RPC. For example:
- The node executing RPC will pulse heart beats to a channel with a key such as `NodeSendingRequest:GUID`
- The node sending the request to the service will pulse heart beats to a channel with a key such as: `NodeReceivingRequest:GUID`

Now each node can watch for heart beats from the other node, when heart beats stop being sent they can assume it is offline and cancel/abandon the request.

## Dealing with Redis losing its data.

Since redis can lose data at anytime the queue is able to detect data lose and cancel any inflight requests when data lose occurs.

## Message serialisation

Message serialisation is provided by re-using the serialiser halibut uses for transferring requests/responses over the wire.

## Cleanup of old data in Redis.

All values in redis have a TTL applied, so redis will automatically clean up old keys if Halibut does not.

Request message TTL: request pickup timeout + 2 minutes.
Response TTL: default 20 minutes.
Pending GUID list TTL: 1 day.
Heartbeat rates: 15s; timeouts: sender 90s, processor 60s.

### DataStream

DataStreams are not stored in the queue, instead an implementation of `IStoreDataStreamsForDistributedQueues` must be provided. It will be called with the DataStreams that are to be stored, and will be called again with the "husks" of a DataStream that needs to be re-hydrated. DataStreams have unique GUIDs which make it easier to find the data for re-hydration.

Sub classing DataStream is a useful technique for avoiding the storage of DataStream data when it is trivial to read the data from some known places. For example a DataStream might be subclassed to hold the file location on disk that should be read when sending the data for a data stream. The halibut serialiser has been updated to work with sub classes of DataStream, in that it will ignore the sub class and send just the DataStream across the wire. This makes it safe to sub class DataStream for efficient storage and have that work with both listening and polling clients.
