docker run -v `pwd`/redis-conf:/usr/local/etc/redis -p 6379:6379 --name redis -d redis redis-server /usr/local/etc/redis/redis.conf

