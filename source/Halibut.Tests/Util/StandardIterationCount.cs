using System;
using Halibut.Tests.Support;

namespace Halibut.Tests.Util
{
    public class StandardIterationCount
    {
        public static int ForServiceType(ServiceConnectionType connectionType)
        {
            switch (connectionType)
            {
                case ServiceConnectionType.Polling:
                    // Polling is slow, we don't know why
                    return 50;
                case ServiceConnectionType.PollingOverWebSocket:
                    // Assume polling over websockets is also slow
                    return 50;
                case ServiceConnectionType.Listening:
                    // Listening is fast
                    return 1000;
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }
    }
}