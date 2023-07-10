using System.Collections;
using System.Collections.Generic;

namespace Halibut.Tests.Support.TestAttributes
{
    public class ServiceConnectionTypesToTestExcludingWebSockets : IEnumerable<ServiceConnectionType>
    {
        public IEnumerator<ServiceConnectionType> GetEnumerator()
        {
            yield return ServiceConnectionType.Polling;
            yield return ServiceConnectionType.Listening;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}