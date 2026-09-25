# Running Redis locally

How to run a Redis for the [Redis Pending Request Queue](RedisQueue.md) on your machine, e.g. to run the tests.

The aim of this setup is a Redis that never stores any data, only keeping it in memory. If Redis restarted and loaded data it had saved earlier, the queue would be rolled back to an older state, which could result in replayed messages or the queue failing. With no saved data, a restart leaves Redis empty instead. The queue detects that and cancels any in-flight requests (see [Dealing with Redis losing its data](RedisQueue.md#dealing-with-redis-losing-its-data)).

Redis can be started by running the following command in the root of the repository:

```
docker run -d --rm --name halibut-redis \
  -p 6379:6379 \
  --user 1001:1001 \
  --read-only \
  --cap-drop ALL \
  --security-opt no-new-privileges \
  --tmpfs /data \
  --tmpfs /tmp \
  -v `pwd`/redis-conf:/etc/redis:ro \
  redis:8.0.3 \
  /etc/redis/redis.conf --requirepass halibut-local-redis
```

This is how we recommend running Redis for the queue. Only the config file and the password are needed for the queue to work. The rest harden the container and are recommended rather than required.

| Option | Why it is set | Required? |
|---|---|---|
| `-d --rm --name halibut-redis` | Runs Redis in the background, removes the container when it stops, and gives it a name so it is easy to stop (`docker stop halibut-redis`). | No, convenience only. |
| `-p 6379:6379` | Exposes Redis on `localhost:6379`, where the tests look for it by default. | Yes, unless you set `HALIBUT_REDIS_PORT` (see below). |
| `--user 1001:1001` | Runs Redis as a non-root user. `1001` is just an example, any non-root user works. The image's entrypoint normally starts as root and then switches to the `redis` user, but that needs capabilities `--cap-drop ALL` removes, so without `--user` Redis runs as root. | No, but we recommend not running as root. |
| `--read-only` | Makes the container's root filesystem read-only, so Redis cannot write anywhere except the tmpfs mounts below. | No, hardening. |
| `--cap-drop ALL` | Removes all Linux capabilities from the container. Redis needs none of them. | No, hardening. |
| `--security-opt no-new-privileges` | Stops processes in the container from gaining privileges, e.g. through setuid binaries. | No, hardening. |
| `--tmpfs /data` | `/data` is Redis's working directory (`dir /data` in `redis.conf`). With `--read-only` it needs a writable directory, and tmpfs keeps it in memory so nothing is ever written to disk. That way Redis never stores any data (see above). | Yes if you use `--read-only`. |
| `--tmpfs /tmp` | Gives Redis writable scratch space in memory, since the root filesystem is read-only. | Yes if you use `--read-only`. |
| ``-v `pwd`/redis-conf:/etc/redis:ro`` | Mounts `redis-conf/redis.conf` read-only. This is where persistence is turned off. The user Redis runs as (see `--user`) must be able to read this file, otherwise Redis fails to start. | Yes. |
| `redis:8.0.3` | The Redis version the queue is tested against. | No, but recommended. |
| `/etc/redis/redis.conf` | Tells Redis to use the mounted config file. | Yes. |
| `--requirepass halibut-local-redis` | Requires clients to authenticate. `halibut-local-redis` is the password the tests look for by default. | No, but recommended. |

We recommend always running Redis with a password, e.g. `--requirepass my-secure-password`, and outside of local testing use a strong, secret password rather than `halibut-local-redis`.

## Running the tests

The tests use a Redis on `localhost:6379` if it accepts the password `halibut-local-redis`. Set `HALIBUT_REDIS_HOST`, `HALIBUT_REDIS_PORT` and `HALIBUT_REDIS_PASSWORD` to use a different Redis. A Redis that does not require a password is ignored, since we recommend Redis always requires one.
