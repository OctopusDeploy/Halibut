using System;

namespace Halibut.Tests.TestSetup.Redis
{
    public static class RedisTestHost
    {
        /// <summary>
        /// Octopus Server uses database 1 in Octopus Cloud (Connection Relay shares the same Redis on database 0).
        /// </summary>
        public const int DefaultDatabase = 1;

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

        public static string Password { get; set; } = "";

        public static string ConnectionString(string host, int port, string password)
        {
            return $"{host}:{port},defaultDatabase={DefaultDatabase},password={password}";
        }
    }
}
