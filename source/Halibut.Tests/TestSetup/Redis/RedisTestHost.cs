using System;

namespace Halibut.Tests.TestSetup.Redis
{
    public static class RedisTestHost
    {
        static int redisPort = 0;
        public static void SetPort(int value)
        {
            redisPort = value;  
        }

        public static int Port()
        {
            if (redisPort == 0)
            {
                throw new Exception("Redis is unavailable");
            }

            return redisPort;
        }

        public static string RedisHost { get; set; } = "localhost";
    }
}