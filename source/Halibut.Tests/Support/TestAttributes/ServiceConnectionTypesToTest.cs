using System.Collections;
using System.Collections.Generic;

namespace Halibut.Tests.Support.TestAttributes
{
    public class ServiceConnectionTypesToTest : IEnumerable<ServiceConnectionType>
    {
        public IEnumerator<ServiceConnectionType> GetEnumerator()
        {
            return ((IEnumerable<ServiceConnectionType>)ServiceConnectionTypes.All).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}