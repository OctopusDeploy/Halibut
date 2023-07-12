using System.Collections;
using System.Collections.Generic;

namespace Halibut.Tests.Support.TestAttributes
{
    public class ServiceConnectionTypesToTest : IEnumerable<ServiceConnectionType>
    {
        public IEnumerator<ServiceConnectionType> GetEnumerator()
        {
            yield return ServiceConnectionType.Polling;
            yield return ServiceConnectionType.Listening;

#if SUPPORTS_WEB_SOCKET_CLIENT
            // Removed for now since it is so unreliable
            //yield return ServiceConnectionType.PollingOverWebSocket;
#endif
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}