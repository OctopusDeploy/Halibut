using System.Collections;
using System.Collections.Generic;
using Halibut.Tests.Support;

namespace Halibut.Tests.Support.TestAttributes
{
    public class ServiceConnectionTypesToTest : IEnumerable<ServiceConnectionType>
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