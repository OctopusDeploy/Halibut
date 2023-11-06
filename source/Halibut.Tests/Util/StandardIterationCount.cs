using System;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestCases;

namespace Halibut.Tests.Util
{
    public class StandardIterationCount
    {
        public static int ForServiceType(ServiceConnectionType connectionType, ClientAndServiceTestVersion clientAndServiceTestVersion)
        {
            if (clientAndServiceTestVersion.IsPreviousClient())
            {
                // Old client requires that we call a halibut which calls a halibut, so keep the iterations low for this one.
                return 10;
            }
            
            switch (connectionType)
            {
                case ServiceConnectionType.Polling:
                    // Polling is slow, we don't know why
                    return 50;
                case ServiceConnectionType.PollingOverWebSocket:
                    // Assume polling over websockets is also slow
                    return 50;
                case ServiceConnectionType.Listening:
                    // Listening is fast on windows.
                    if (OperatingSystem.IsWindows())
                    {
                        return 1000;
                    }
                    // It is not clear why listening is slower on linux.
                    return 250;
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }
    }
}