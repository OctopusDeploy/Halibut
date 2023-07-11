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
                    return 100;
                case ServiceConnectionType.PollingOverWebSocket:
                    // Assume polling over websockets is also slow
                    return 100;
                case ServiceConnectionType.Listening:
                    // Listening is fast
                    return 2000;
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }
    }
}