using System.Collections.Generic;
using System.Runtime.InteropServices;

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
        public static ServiceConnectionType[] All
        {
            get
            {
                var all = new List<ServiceConnectionType>
                {
                    ServiceConnectionType.Listening,
                    ServiceConnectionType.Polling
                };

                if (CanRunWebSockets())
                {
                    all.Add(ServiceConnectionType.PollingOverWebSocket);
                }

                return all.ToArray();
            }
        }

        public static ServiceConnectionType[] AllExceptWebSockets => new[]
        {
            ServiceConnectionType.Listening,
            ServiceConnectionType.Polling
        };

        static bool CanRunWebSockets()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
    }
}