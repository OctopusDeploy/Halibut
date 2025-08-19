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

