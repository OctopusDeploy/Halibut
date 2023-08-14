namespace Halibut.Tests.Support
{
    public enum ServiceConnectionType
    {
        Polling,
        PollingOverWebSocket,
        Listening
    }

    public static class ServiceConnectionTypes
    {
        public static ServiceConnectionType[] All => new[]
        {
            ServiceConnectionType.Listening,
            ServiceConnectionType.Polling,
            ServiceConnectionType.PollingOverWebSocket
        };

        public static ServiceConnectionType[] AllExceptWebSockets => new[]
        {
            ServiceConnectionType.Listening,
            ServiceConnectionType.Polling
        };

    }
}