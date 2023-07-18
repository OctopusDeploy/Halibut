using System;
using System.Collections;
using System.Collections.Generic;

namespace Halibut.Tests.Support.TestAttributes
{
    public class PollingServiceConnectionTypesToTest : IEnumerable<ServiceConnectionType>
    {
        public IEnumerator<ServiceConnectionType> GetEnumerator()
        {
            var toTest = new List<ServiceConnectionType>
            {
                ServiceConnectionType.Polling,
// Disabled while these are causing flakey Team City Tests
//#if SUPPORTS_WEB_SOCKET_CLIENT
//             ServiceConnectionType.PollingOverWebSocket
//#endif
            };
            return ((IEnumerable<ServiceConnectionType>) toTest).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}